import socket
import re
import signal
import sys
import threading

# --- Configuration ---
HOST = "127.0.0.1"  # Listen on localhost
PORT = 4567        # Default IPK25-CHAT port
BUFFER_SIZE = 4096 # Reasonably large buffer
CRLF = "\r\n"
ENCODING = 'us-ascii' # As specified in the protocol

# --- Pre-compiled Regex Patterns (Based on ABNF) ---
# Note: These are simplified for parsing, not strict validation
#       They assume the client sends correctly formatted messages for basic testing.
RE_AUTH = re.compile(r"^AUTH\s+([a-zA-Z0-9_-]{1,20})\s+AS\s+([\x21-\x7E]{1,20})\s+USING\s+([a-zA-Z0-9_-]{1,128})$")
RE_JOIN = re.compile(r"^JOIN\s+([a-zA-Z0-9_-]{1,20})\s+AS\s+([\x21-\x7E]{1,20})$")
RE_MSG = re.compile(r"^MSG\s+FROM\s+([\x21-\x7E]{1,20})\s+IS\s+(.*)$", re.DOTALL) # DOTALL for multiline messages potentially ending before CRLF
RE_BYE = re.compile(r"^BYE\s+FROM\s+([\x21-\x7E]{1,20})$")

# --- Global variable for the server socket ---
server_socket = None

def send_message(sock, message):
    """Encodes and sends a message with CRLF."""
    try:
        print(f"SND: {message}")
        sock.sendall((message + CRLF).encode(ENCODING))
    except OSError as e:
        print(f"Error sending message: {e}")
    except Exception as e:
        print(f"An unexpected error occurred during send: {e}")


def handle_client(conn, addr):
    """Handles a single client connection."""
    print(f"Connection accepted from {addr}")
    client_state = "NEEDS_AUTH" # Initial state
    display_name = None
    current_channel = "default" # Initial channel after auth
    incoming_buffer = ""

    try:
        while True:
            # --- Receive Data ---
            try:
                data = conn.recv(BUFFER_SIZE)
                if not data:
                    print(f"Client {addr} disconnected unexpectedly.")
                    break # Connection closed by client

                incoming_buffer += data.decode(ENCODING)
            except UnicodeDecodeError:
                 print(f"RCV from {addr}: Invalid {ENCODING} data. Sending ERR.")
                 send_message(conn, f"ERR FROM Server IS Invalid character encoding")
                 break
            except OSError as e:
                 print(f"Error receiving data from {addr}: {e}")
                 break # Socket error

            # --- Process Complete Messages ---
            while CRLF in incoming_buffer:
                message, incoming_buffer = incoming_buffer.split(CRLF, 1)
                print(f"RCV from {addr}: {message}")

                # --- State Machine Logic ---
                if client_state == "NEEDS_AUTH":
                    match = RE_AUTH.match(message)
                    if match:
                        username, received_dname, secret = match.groups()
                        # Basic validation simulation (in real server, check credentials)
                        print(f"AUTH attempt: User='{username}', Display='{received_dname}', Secret='{secret[:5]}...'")
                        display_name = received_dname # Store display name
                        send_message(conn, "REPLY OK IS Auth success.")
                        # According to FSM, server joins client to default channel implicitly
                        send_message(conn, f"MSG FROM Server IS {display_name} joined {current_channel}.")
                        client_state = "AUTHENTICATED"
                        print(f"Client {addr} authenticated as '{display_name}'. State -> AUTHENTICATED")
                    else:
                        print(f"Invalid AUTH format from {addr} or wrong state.")
                        send_message(conn, "REPLY NOK IS Authentication failed or bad format.")
                        # Keep state as NEEDS_AUTH or terminate? Spec implies ERR leads to termination.
                        # Let's send ERR for protocol violation.
                        send_message(conn, f"ERR FROM Server IS Malformed AUTH message or wrong state.")
                        return # Terminate handler for this client

                elif client_state == "AUTHENTICATED":
                    # --- Handle JOIN ---
                    match = RE_JOIN.match(message)
                    if match:
                        channel_id, received_dname = match.groups()
                        # The client sends its display name again, maybe for consistency?
                        # A real server might validate if received_dname matches the stored one.
                        if received_dname != display_name:
                             print(f"WARN: JOIN display name '{received_dname}' differs from authenticated '{display_name}'")
                             # Let's allow it for testing flexibility, but log a warning.
                             # Or send ERR? Let's send ERR for stricter testing.
                             #send_message(conn, f"ERR FROM Server IS JOIN DisplayName mismatch.")
                             #return
                        print(f"JOIN attempt: Channel='{channel_id}', Display='{received_dname}'")
                        current_channel = channel_id # Update current channel
                        send_message(conn, "REPLY OK IS Join success.")
                        send_message(conn, f"MSG FROM Server IS {display_name} joined {current_channel}.")
                        print(f"Client '{display_name}' joined channel '{current_channel}'.")
                        continue # Process next message if any

                    # --- Handle MSG ---
                    match = RE_MSG.match(message)
                    if match:
                        sender_dname, msg_content = match.groups()
                         # Check if sender name matches authenticated name
                        if sender_dname != display_name:
                            print(f"WARN: MSG FROM display name '{sender_dname}' differs from authenticated '{display_name}'")
                            # Decide whether to reject (ERR) or just log. Let's reject.
                            send_message(conn, f"ERR FROM Server IS MSG DisplayName mismatch.")
                            return # Terminate handler

                        print(f"MSG received: From='{sender_dname}', Content='{msg_content[:50]}...'")
                        # In a real server, you'd broadcast this to others in the channel.
                        # Here, we just acknowledge receipt on the server side.
                        # No REPLY is sent for MSG according to the spec.
                        continue # Process next message if any

                    # --- Handle BYE ---
                    match = RE_BYE.match(message)
                    if match:
                        sender_dname = match.group(1)
                        if sender_dname != display_name:
                             print(f"WARN: BYE FROM display name '{sender_dname}' differs from authenticated '{display_name}'")
                             # Rejecting BYE might prevent graceful close. Let's just log and accept.
                        print(f"BYE received from '{sender_dname}'. Closing connection.")
                        # No confirmation needed for BYE in TCP.
                        return # Exit the handler loop gracefully

                    # --- Handle Unknown / Malformed ---
                    print(f"Unknown or malformed message from {addr} in AUTHENTICATED state: {message}")
                    send_message(conn, f"ERR FROM Server IS Unknown command or malformed message.")
                    return # Terminate handler

                else: # Should not happen
                    print(f"FATAL: Unknown client state '{client_state}' for {addr}.")
                    return # Terminate handler

    except ConnectionResetError:
        print(f"Client {addr} reset the connection.")
    except BrokenPipeError:
         print(f"Client {addr} connection broken.")
    except Exception as e:
        print(f"An unexpected error occurred with client {addr}: {e}")
        # Try sending an ERR if the socket is still usable
        try:
            send_message(conn, f"ERR FROM Server IS An internal server error occurred.")
        except Exception:
            pass # Ignore if sending fails now
    finally:
        print(f"Closing connection to {addr}")
        conn.close()


def signal_handler(sig, frame):
    """Handles Ctrl+C for graceful shutdown."""
    global server_socket
    print("\nShutting down server...")
    if server_socket:
        server_socket.close()
        print("Server socket closed.")
    sys.exit(0)


def start_server():
    """Starts the TCP chat server."""
    global server_socket
    # --- Set up signal handler ---
    signal.signal(signal.SIGINT, signal_handler)

    # --- Create and Bind Socket ---
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    # Allow reusing the address shortly after closing (useful for development)
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)

    try:
        server_socket.bind((HOST, PORT))
        server_socket.listen()
        print(f"TCP Server listening on {HOST}:{PORT}...")
    except OSError as e:
        print(f"Error binding or listening: {e}")
        sys.exit(1)
    except Exception as e:
         print(f"An unexpected error occurred during setup: {e}")
         sys.exit(1)


    # --- Accept Connections Loop ---
    try:
        while True:
            try:
                conn, addr = server_socket.accept()
                # Handle each client in a separate thread (simple approach)
                # Warning: For many clients, a different concurrency model (like asyncio or select) is better.
                client_thread = threading.Thread(target=handle_client, args=(conn, addr))
                client_thread.daemon = True # Allows main thread to exit even if client threads are running
                client_thread.start()
                # Note: Since we handle one client at a time *per thread*,
                # the handle_client function itself is blocking for that specific client.
            except OSError:
                # This might happen if the socket is closed by the signal handler
                print("Server socket closed, exiting accept loop.")
                break
            except Exception as e:
                 print(f"Error accepting connection: {e}")
                 # Continue trying to accept new connections unless it's a fatal error

    finally:
        if server_socket:
            server_socket.close()
            print("Server socket closed.")

if __name__ == "__main__":
    start_server()