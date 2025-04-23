import socket
import struct
import sys
import signal
import threading
import time
import select
import random

# --- Configuration ---
HOST = "127.0.0.1"  # Listen on localhost
PORT = 4567        # Default IPK25-CHAT port for AUTH
BUFFER_SIZE = 65535 # Max UDP datagram size technically possible (though MTU limits practical size)
ENCODING = 'us-ascii' # As specified in the protocol
PING_INTERVAL = 15 # Send PING every 15 seconds (example)
CLIENT_TIMEOUT = 60 # Remove inactive client session after 60 seconds (example)


# --- Message Types (from spec) ---
MSG_TYPE_CONFIRM = 0x00
MSG_TYPE_REPLY   = 0x01
MSG_TYPE_AUTH    = 0x02
MSG_TYPE_JOIN    = 0x03
MSG_TYPE_MSG     = 0x04
MSG_TYPE_PING    = 0xFD
MSG_TYPE_ERR     = 0xFE
MSG_TYPE_BYE     = 0xFF

# --- Session Management ---
client_sessions = {} # Key: client_addr (ip, port), Value: SessionData object
sessions_lock = threading.Lock() # To protect access to client_sessions

# --- Sockets for select ---
main_socket = None
monitored_sockets = [] # List of sockets to monitor with select() [main_socket, dynamic_socket1, ...]
socket_to_session = {} # Map socket object back to client_addr for dynamic sockets

class SessionData:
    """Stores state for an active client session."""
    def __init__(self, client_addr, dynamic_socket):
        self.client_addr = client_addr
        self.socket = dynamic_socket # The dynamic socket for this session
        self.state = "NEEDS_AUTH" # Initial state before REPLY is sent
        self.display_name = None
        self.current_channel = "default"
        self.server_message_id = 0 # Counter for messages sent BY SERVER
        self.received_client_message_ids = set() # IDs received FROM CLIENT
        self.last_activity_time = time.time()
        self.last_ping_time = time.time()
        self.pending_auth_reply = None # Store details needed for AUTH REPLY after CONFIRM

    def get_next_server_msg_id(self):
        msg_id = self.server_message_id
        self.server_message_id = (self.server_message_id + 1) % 65536 # Wrap around uint16
        return msg_id

    def add_received_client_id(self, msg_id):
        self.received_client_message_ids.add(msg_id)

    def has_received_client_id(self, msg_id):
        return msg_id in self.received_client_message_ids

    def update_activity(self):
        self.last_activity_time = time.time()


# --- Message Packing / Unpacking Helpers ---

def pack_confirm(ref_message_id):
    # CONFIRM has no MessageID of its own, only Ref_MessageID
    return struct.pack('>BH', MSG_TYPE_CONFIRM, ref_message_id)

def pack_reply(message_id, result, ref_message_id, content):
    # >B H B H s x (Type, MsgID, Result, RefMsgID, Content, Null)
    encoded_content = content.encode(ENCODING)
    fmt = f'>BHBH{len(encoded_content)}sx' # x for the null terminator
    return struct.pack(fmt, MSG_TYPE_REPLY, message_id, result, ref_message_id, encoded_content)

def pack_auth(message_id, username, display_name, secret):
    # >B H s x s x s x
    enc_user = username.encode(ENCODING)
    enc_display = display_name.encode(ENCODING)
    enc_secret = secret.encode(ENCODING)
    fmt = f'>BH{len(enc_user)}sx{len(enc_display)}sx{len(enc_secret)}sx'
    return struct.pack(fmt, MSG_TYPE_AUTH, message_id, enc_user, enc_display, enc_secret)

def pack_join(message_id, channel_id, display_name):
     # >B H s x s x
    enc_channel = channel_id.encode(ENCODING)
    enc_display = display_name.encode(ENCODING)
    fmt = f'>BH{len(enc_channel)}sx{len(enc_display)}sx'
    return struct.pack(fmt, MSG_TYPE_JOIN, message_id, enc_channel, enc_display)

def pack_msg(message_id, display_name, content):
    # >B H s x s x
    enc_display = display_name.encode(ENCODING)
    enc_content = content.encode(ENCODING)
    fmt = f'>BH{len(enc_display)}sx{len(enc_content)}sx'
    return struct.pack(fmt, MSG_TYPE_MSG, message_id, enc_display, enc_content)

def pack_err(message_id, display_name, content):
    # Same structure as MSG
    enc_display = display_name.encode(ENCODING)
    enc_content = content.encode(ENCODING)
    fmt = f'>BH{len(enc_display)}sx{len(enc_content)}sx'
    return struct.pack(fmt, MSG_TYPE_ERR, message_id, enc_display, enc_content)

def pack_bye(message_id, display_name):
    # >B H s x
    enc_display = display_name.encode(ENCODING)
    fmt = f'>BH{len(enc_display)}sx'
    return struct.pack(fmt, MSG_TYPE_BYE, message_id, enc_display)

def pack_ping(message_id):
    # >B H
    return struct.pack('>BH', MSG_TYPE_PING, message_id)

def parse_udp_message(data):
    """Parses the header and identifies the type. Returns (type, msg_id, remaining_data) or None."""
    if len(data) < 3:
        return None # Not enough data for header
    msg_type, msg_id = struct.unpack('>BH', data[:3])
    return msg_type, msg_id, data[3:]

def unpack_string_from(data):
    """Finds the first null byte and returns the decoded string and remaining data."""
    try:
        null_pos = data.index(b'\x00')
        value = data[:null_pos].decode(ENCODING)
        remaining = data[null_pos+1:]
        return value, remaining
    except (ValueError, UnicodeDecodeError): # Null byte not found or decode error
        return None, data


# --- Server Logic ---

def send_udp(sock, message_bytes, target_addr):
    """Sends UDP data and logs it."""
    try:
        # Simple logging: Type and maybe MsgID if not CONFIRM
        msg_type = message_bytes[0]
        type_name = {
            0x00: "CONFIRM", 0x01: "REPLY", 0x02: "AUTH", 0x03: "JOIN",
            0x04: "MSG", 0xFD: "PING", 0xFE: "ERR", 0xFF: "BYE"
        }.get(msg_type, f"UNKNOWN(0x{msg_type:02X})")

        if msg_type == MSG_TYPE_CONFIRM and len(message_bytes) >= 3:
            ref_id = struct.unpack('>H', message_bytes[1:3])[0]
            print(f"SND to {target_addr}: {type_name} (RefID: {ref_id})")
        elif len(message_bytes) >= 3:
             msg_id = struct.unpack('>H', message_bytes[1:3])[0]
             print(f"SND to {target_addr}: {type_name} (MsgID: {msg_id})")
        else:
             print(f"SND to {target_addr}: {type_name} (Malformed header?)")

        sock.sendto(message_bytes, target_addr)
    except OSError as e:
        print(f"Error sending UDP to {target_addr}: {e}")
    except Exception as e:
        print(f"Unexpected error sending UDP: {e}")


def handle_auth(data, client_addr):
    """Handles an AUTH message received on the main socket."""
    global monitored_sockets, socket_to_session

    parsed = parse_udp_message(data)
    if not parsed or parsed[0] != MSG_TYPE_AUTH:
        print(f"RCV from {client_addr} on main: Ignoring non-AUTH or malformed message.")
        return # Ignore non-AUTH on main port

    msg_type, msg_id, content = parsed
    print(f"RCV from {client_addr}: AUTH (MsgID: {msg_id})")

    # --- Check for existing session trying to re-AUTH ---
    # A simple approach: If already exists, just confirm the AUTH again.
    # A stricter server might reject re-authentication attempts.
    with sessions_lock:
        if client_addr in client_sessions:
            session = client_sessions[client_addr]
            print(f"Client {client_addr} already has session, confirming AUTH {msg_id} again from main socket.")
            if not session.has_received_client_id(msg_id):
                 session.add_received_client_id(msg_id) # Track it even if re-auth
                 session.update_activity()
            # Send CONFIRM from the *main* socket as per spec diagram for initial AUTH
            confirm_msg = pack_confirm(msg_id)
            send_udp(main_socket, confirm_msg, client_addr)
            return # Don't create a new session

    # --- Parse AUTH content ---
    username, remaining = unpack_string_from(content)
    if username is None: return print_err("Malformed AUTH: Cannot parse Username")
    display_name, remaining = unpack_string_from(remaining)
    if display_name is None: return print_err("Malformed AUTH: Cannot parse DisplayName")
    secret, remaining = unpack_string_from(remaining)
    if secret is None: return print_err("Malformed AUTH: Cannot parse Secret")

    print(f"AUTH details: User='{username}', Display='{display_name}', Secret='{secret[:5]}...'")

    # --- Create Dynamic Socket and Session ---
    try:
        dynamic_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        dynamic_sock.bind((HOST, 0)) # Bind to ephemeral port
        dynamic_port = dynamic_sock.getsockname()[1]
        print(f"Allocated dynamic port {dynamic_port} for {client_addr}")
    except OSError as e:
        print(f"Error creating dynamic socket for {client_addr}: {e}")
        # Maybe send ERR back from main socket? Difficult without established session.
        return

    # --- Store Session ---
    session = SessionData(client_addr, dynamic_sock)
    session.display_name = display_name # Store display name now
    session.add_received_client_id(msg_id) # Mark AUTH msg_id as received
    session.pending_auth_reply = { # Store details for REPLY after CONFIRM
        "ref_msg_id": msg_id,
        "content": "Auth success."
    }

    with sessions_lock:
        client_sessions[client_addr] = session
        monitored_sockets.append(dynamic_sock) # Add new socket to select list
        socket_to_session[dynamic_sock] = client_addr # Map socket back to client

    # --- Send CONFIRM for AUTH (from main socket) ---
    confirm_msg = pack_confirm(msg_id)
    send_udp(main_socket, confirm_msg, client_addr)

    # --- Send REPLY for AUTH (from dynamic socket) ---
    # This happens immediately after CONFIRM in this simple server
    reply_msg_id = session.get_next_server_msg_id()
    reply_content = session.pending_auth_reply["content"]
    reply_ref_id = session.pending_auth_reply["ref_msg_id"]
    reply_msg = pack_reply(reply_msg_id, 1, reply_ref_id, reply_content) # 1 = success
    send_udp(session.socket, reply_msg, session.client_addr)
    session.state = "WAITING_REPLY_CONFIRM" # State change after sending REPLY
    session.pending_auth_reply = None
    session.update_activity()
    print(f"Session created for {client_addr}. State -> WAITING_REPLY_CONFIRM")

     # Send the initial "Joined default" MSG right after REPLY
    msg_join_id = session.get_next_server_msg_id()
    msg_join_content = f"{session.display_name} joined {session.current_channel}."
    msg_join = pack_msg(msg_join_id, "Server", msg_join_content)
    send_udp(session.socket, msg_join, session.client_addr)


def handle_dynamic_message(sock, data, client_addr):
    """Handles messages received on a dynamic client socket."""
    session = None
    with sessions_lock:
        # Verify the message source matches the session associated with this socket
        if sock in socket_to_session and socket_to_session[sock] == client_addr:
            session = client_sessions.get(client_addr)

    if not session:
        print(f"RCV on dynamic socket {sock.getsockname()} from {client_addr}: No matching session found. Ignoring.")
        return

    session.update_activity() # Update activity on any valid message

    # --- Handle CONFIRM ---
    if data[0] == MSG_TYPE_CONFIRM:
        if len(data) < 3: return print_err(f"Malformed CONFIRM from {client_addr}")
        ref_msg_id = struct.unpack('>H', data[1:3])[0]
        print(f"RCV from {client_addr}: CONFIRM (RefID: {ref_msg_id})")
        # Server doesn't explicitly wait for confirms in this simple model,
        # but we can use this to transition state, e.g., after sending REPLY.
        if session.state == "WAITING_REPLY_CONFIRM": # Assuming RefID matches the REPLY msg ID
             print(f"Authentication for {client_addr} confirmed. State -> AUTHENTICATED")
             session.state = "AUTHENTICATED"
        # Handle confirm for ERR/BYE if needed for cleanup states
        elif session.state == "SENT_ERR" or session.state == "SENT_BYE":
             print(f"CONFIRM received for final message from {client_addr}. Ready for cleanup.")
             # Mark session for cleanup? Or just let timeout handle it.
        return # No further processing for CONFIRM

    # --- Parse Non-CONFIRM Message ---
    parsed = parse_udp_message(data)
    if not parsed:
        print(f"RCV from {client_addr}: Malformed message header.")
        # Send ERR? Need a message ID from the client to confirm. Difficult. Ignore for now.
        return

    msg_type, msg_id, content = parsed

    # --- Duplicate Check ---
    if session.has_received_client_id(msg_id):
        print(f"RCV from {client_addr}: Duplicate message (Type: 0x{msg_type:02X}, MsgID: {msg_id}). Sending CONFIRM only.")
        confirm_msg = pack_confirm(msg_id)
        send_udp(session.socket, confirm_msg, session.client_addr)
        return # Stop processing duplicate

    # --- Add new message ID to received set ---
    session.add_received_client_id(msg_id)
    print(f"RCV from {client_addr}: Type=0x{msg_type:02X}, MsgID={msg_id}")

    # --- Send CONFIRM for the new message ---
    confirm_msg = pack_confirm(msg_id)
    send_udp(session.socket, confirm_msg, session.client_addr)

    # --- Process based on Type and State ---
    if session.state == "AUTHENTICATED":
        if msg_type == MSG_TYPE_JOIN:
            channel_id, remaining = unpack_string_from(content)
            if channel_id is None: return print_err(f"Malformed JOIN from {client_addr}: Cannot parse ChannelID")
            display_name_join, remaining = unpack_string_from(remaining) # Client sends this again
            if display_name_join is None: return print_err(f"Malformed JOIN from {client_addr}: Cannot parse DisplayName")
            if display_name_join != session.display_name:
                print(f"WARN: JOIN DisplayName '{display_name_join}' mismatch for {client_addr}")
                # Send ERR? Let's allow it but log.
                # err_msg_id = session.get_next_server_msg_id()
                # err_msg = pack_err(err_msg_id, "Server", "JOIN DisplayName mismatch.")
                # send_udp(session.socket, err_msg, session.client_addr)
                # session.state = "SENT_ERR" # Or maybe allow continue? For testing, let's allow.

            print(f"Client {client_addr} joining channel '{channel_id}'")
            session.current_channel = channel_id

            # Send REPLY for JOIN
            reply_msg_id = session.get_next_server_msg_id()
            reply_msg = pack_reply(reply_msg_id, 1, msg_id, "Join success.") # 1=OK, ref_id=JOIN msg id
            send_udp(session.socket, reply_msg, session.client_addr)

            # Send MSG notification
            msg_join_id = session.get_next_server_msg_id()
            msg_join_content = f"{session.display_name} joined {session.current_channel}."
            msg_join = pack_msg(msg_join_id, "Server", msg_join_content)
            send_udp(session.socket, msg_join, session.client_addr)


        elif msg_type == MSG_TYPE_MSG:
            display_name_msg, remaining = unpack_string_from(content)
            if display_name_msg is None: return print_err(f"Malformed MSG from {client_addr}: Cannot parse DisplayName")
            message_content, remaining = unpack_string_from(remaining)
            if message_content is None: return print_err(f"Malformed MSG from {client_addr}: Cannot parse MessageContent")

            if display_name_msg != session.display_name:
                 print(f"WARN: MSG DisplayName '{display_name_msg}' mismatch for {client_addr}")
                 # Send ERR? Let's allow and log for testing.

            print(f"MSG from {client_addr} ({display_name_msg}) in '{session.current_channel}': {message_content[:60]}...")
            # In real server: find other clients in session.current_channel and relay
            # Here: Just acknowledge receipt by having sent CONFIRM.

        elif msg_type == MSG_TYPE_BYE:
            display_name_bye, remaining = unpack_string_from(content)
            if display_name_bye is None: return print_err(f"Malformed BYE from {client_addr}: Cannot parse DisplayName")
            print(f"BYE received from {client_addr} ({display_name_bye}). Session will be terminated.")
            # Mark session for termination. CONFIRM already sent.
            terminate_session(client_addr, sock, "Client sent BYE")


        #elif msg_type == MSG_TYPE_PING: # Client shouldn't send PING, but handle defensively
        #    print(f"Received PING from client {client_addr}? Ignoring.")

        else:
            print(f"Unhandled message type 0x{msg_type:02X} from {client_addr} in AUTHENTICATED state.")
            # Send ERR
            err_msg_id = session.get_next_server_msg_id()
            err_msg = pack_err(err_msg_id, "Server", f"Invalid message type {msg_type} in current state.")
            send_udp(session.socket, err_msg, session.client_addr)
            terminate_session(client_addr, sock, "Sent ERR for invalid message type")


    elif session.state == "WAITING_REPLY_CONFIRM":
         # Should only receive CONFIRM here ideally, but handle others defensively
         print(f"Received message type 0x{msg_type:02X} from {client_addr} while waiting for AUTH REPLY CONFIRM. Ignoring.")
         # Maybe send ERR if it's not a duplicate of the original AUTH?
         # For simplicity, ignore now. CONFIRM was already sent for this new message ID.

    else: # Should not happen (e.g., SENT_ERR, SENT_BYE states)
        print(f"Received message type 0x{msg_type:02X} from {client_addr} in unexpected state {session.state}. Ignoring.")


def print_err(message):
    """Helper to print error messages."""
    print(f"ERROR: {message}")
    return None # To allow "return print_err(...)"


def send_pings():
    """Iterates through sessions and sends PING if needed."""
    with sessions_lock:
        now = time.time()
        # Iterate over a copy of keys in case terminate_session modifies the dict
        for client_addr in list(client_sessions.keys()):
            session = client_sessions.get(client_addr)
            if session and session.state == "AUTHENTICATED":
                if now - session.last_ping_time > PING_INTERVAL:
                    ping_msg_id = session.get_next_server_msg_id()
                    ping_msg = pack_ping(ping_msg_id)
                    print(f"Sending PING (MsgID: {ping_msg_id}) to {client_addr}")
                    send_udp(session.socket, ping_msg, session.client_addr)
                    session.last_ping_time = now


def cleanup_inactive_sessions():
    """Removes sessions that haven't seen activity."""
    with sessions_lock:
        now = time.time()
        inactive_clients = []
        for client_addr, session in client_sessions.items():
            if now - session.last_activity_time > CLIENT_TIMEOUT:
                inactive_clients.append((client_addr, session.socket))

        for client_addr, sock in inactive_clients:
             terminate_session(client_addr, sock, "Session timed out")


def terminate_session(client_addr, sock, reason="Unknown"):
    """Cleans up a client session."""
    global monitored_sockets, socket_to_session
    print(f"Terminating session for {client_addr}: {reason}")
    with sessions_lock:
        if client_addr in client_sessions:
            del client_sessions[client_addr]
        if sock in socket_to_session:
            del socket_to_session[sock]
        if sock in monitored_sockets:
            monitored_sockets.remove(sock)
            try:
                sock.close()
            except OSError as e:
                print(f"Error closing dynamic socket for {client_addr}: {e}")
        else:
             print(f"WARN: Socket for {client_addr} not found in monitored list during termination.")


def signal_handler(sig, frame):
    """Handles Ctrl+C for graceful shutdown."""
    global main_socket
    print("\nShutting down server...")
    # Close all dynamic sockets first
    with sessions_lock:
         for client_addr, session in client_sessions.items():
              print(f"Closing socket for {client_addr}...")
              try:
                   session.socket.close()
              except OSError:
                   pass # Ignore errors on shutdown close
         client_sessions.clear()
         monitored_sockets.clear()
         socket_to_session.clear()

    if main_socket:
        try:
            main_socket.close()
            print("Main server socket closed.")
        except OSError:
             pass # Ignore errors on shutdown close
    sys.exit(0)


def start_server():
    """Starts the UDP chat server."""
    global main_socket, monitored_sockets
    signal.signal(signal.SIGINT, signal_handler)

    try:
        main_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        main_socket.bind((HOST, PORT))
        print(f"UDP Server listening on {HOST}:{PORT}...")
        monitored_sockets.append(main_socket)
    except OSError as e:
        print(f"Error binding main socket: {e}")
        sys.exit(1)

    last_ping_check_time = time.time()
    last_cleanup_time = time.time()

    while True:
        try:
            # Use select with a timeout to allow periodic tasks
            readable, _, exceptional = select.select(monitored_sockets, [], monitored_sockets, 1.0)

            for sock in readable:
                try:
                    data, addr = sock.recvfrom(BUFFER_SIZE)
                    if not data:
                        # UDP doesn't typically signal close like TCP with 0 bytes
                        print(f"Received 0 bytes from {addr} on {sock.getsockname()}? Ignoring.")
                        continue

                    if sock == main_socket:
                        handle_auth(data, addr)
                    else: # Must be a dynamic client socket
                        handle_dynamic_message(sock, data, addr)

                except ConnectionResetError: # Important for UDP on Windows
                     print(f"Connection reset by peer {addr} likely means port unreachable. Terminating session.")
                     # Find the session associated with the socket that got the error
                     client_addr_to_remove = None
                     with sessions_lock:
                          for c_addr, sess in client_sessions.items():
                              if sess.socket == sock:
                                  client_addr_to_remove = c_addr
                                  break
                     if client_addr_to_remove:
                         terminate_session(client_addr_to_remove, sock, "ConnectionResetError")
                     else:
                         print(f"WARN: ConnectionResetError on unknown dynamic socket {sock.getsockname()}")

                except OSError as e:
                    print(f"Socket error receiving from {addr} on {sock.getsockname()}: {e}")
                    # Potentially terminate session if it's a dynamic socket?
                    client_addr_to_remove = None
                    with sessions_lock:
                        if sock in socket_to_session:
                            client_addr_to_remove = socket_to_session[sock]
                    if client_addr_to_remove:
                        terminate_session(client_addr_to_remove, sock, f"Socket OSError: {e}")

                except Exception as e:
                    print(f"Unexpected error handling readable socket {sock.getsockname()}: {e}")


            for sock in exceptional:
                print(f"Exceptional condition on socket {sock.getsockname()}. Terminating associated session.")
                client_addr_to_remove = None
                with sessions_lock:
                     if sock in socket_to_session:
                          client_addr_to_remove = socket_to_session[sock]
                if client_addr_to_remove:
                    terminate_session(client_addr_to_remove, sock, "Exceptional socket condition")
                elif sock == main_socket:
                     print("FATAL: Exceptional condition on main socket. Exiting.")
                     signal_handler(signal.SIGINT, None) # Trigger shutdown


            # --- Periodic Tasks ---
            now = time.time()
            if now - last_ping_check_time > 5: # Check every 5 seconds to send pings
                 send_pings()
                 last_ping_check_time = now

            if now - last_cleanup_time > 10: # Check every 10 seconds for inactive clients
                 cleanup_inactive_sessions()
                 last_cleanup_time = now


        except KeyboardInterrupt:
            signal_handler(signal.SIGINT, None)
        except Exception as e:
             print(f"FATAL: Unhandled exception in main loop: {e}")
             signal_handler(signal.SIGINT, None) # Attempt graceful shutdown

if __name__ == "__main__":
    start_server()