using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Ipk25Chat
{
    // This class represents a UDP chat client.
    public class UdpChatClient : ChatClient
    {
        // Static dictionary to store pending confirmation messages
        public static Dictionary<int, UdpSentMessageInfo> _pendingConfirmationMessages = new Dictionary<int, UdpSentMessageInfo>();
        // Static HashSet to track already confirmed message IDs
        public static HashSet<int> alreadyConfirmedIds = new HashSet<int>();

        // timeout and retry settings
        private readonly ushort _timeoutMs;
        private readonly byte _maxRetries;

        // Flag to track authentication status
        private bool _isAuthenticated = false; 
        // Socket and server endpoints
        private Socket? _socket;
        private IPEndPoint? _initialServerEndPoint;
        private IPEndPoint? _dynamicServerEndPoint;

        // Cancellation token source for async operations
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    
        // Message ID counter for sent messages
        private int _nextMessageId = -1;

        // Lock for thread-safe access to message ID
        private readonly object _messageIdLock = new object();
        
        public UdpChatClient(string server, ushort port, ushort timeOutMs, byte maxRetries) : base(server, port)
        {
            _timeoutMs = timeOutMs;
            _maxRetries = maxRetries;
            InitEndpoint();
        }
        
        
        private void InitEndpoint()
        {
            _initialServerEndPoint = new IPEndPoint(_server, _port);
        }
        // Method to get the next message ID in a thread-safe manner
        private int GetNextMessageId()
        {
            lock (_messageIdLock)
            {
                _nextMessageId++;
                return _nextMessageId;
            }
        }
        // Method to get the previous message ID in a thread-safe manner
        private int GetPrevMessageId()
        {
            lock (_messageIdLock)
            {
                if (_nextMessageId > 0)
                {
                    _nextMessageId--;
                }
                return _nextMessageId;
            }
        }
        // Method to initialize the UDP socket
        private void InitializeSocket()
        {
            if (_socket != null)
            {
                Console.Error.WriteLine("DEBUG: Socket is already initialized.");
                return;
            }

            try
            {
                // Create a new UDP socket
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 0);

                // Bind the socket to the local endpoint
                _socket.Bind(localEndPoint);

                Console.Error.WriteLine("DEBUG: Socket initialized successfully.");
            }
            catch (SocketException)
            {
                Console.WriteLine("ERROR: Socket initialization failed.");
                _socket = null;
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Unexpected error during socket initialization: {ex.Message}");
                _socket = null;
                Environment.Exit(1);
            }
        }
        // Method to run the UDP chat client
        public async Task RunAsync()
        {
            InitializeSocket();
            // start receive loop in background
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

            while (CurrentState != ClientState.End)
            {
                try
                {
                    // Read user input asynchronously - return message object based on user input
                    Message? message = UdpMessageParser.CreateMessageFromUserInputAsync(GetNextMessageId(), this);

                    if (message == null)
                    {
                        Console.WriteLine("ERROR: Invalid command. Use /help.");
                        continue;
                    }
                    // Process the message
                    await ProcessMessageAsync(message);
                }
                // handle crtl D (eof) on user input
                catch (ArgumentNullException)
                {
                    await SendByeMessageAsync();
                }
                catch (ArgumentException ex)
                {
                    // rename is not a message - decrement message id
                    if (ex.Message == "rename")
                    {
                        GetPrevMessageId();
                        continue;
                    }
                    if (ex.Message == "help")
                    {
                        PrintHelp();
                    }
                    else
                    {
                        Console.WriteLine("ERROR: " + ex);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex);
                }
            }
        }
        
    
        private async Task SendByeMessageAsync()
        {
            ByeMessage byeMessage = new ByeMessage(DisplayName, GetNextMessageId());
            byte[] payload = byeMessage.GetBytesForUdpPacket();
            SendUdpPayloadToServer(payload);
            await Task.Run(() => WaitConfirm(byeMessage, payload)); // Ensure asynchronous behavior
        }

        private void WaitConfirm(Message message, byte[] payload)
        {
            // Check if the socket is initialized
            IPEndPoint? iPEndpoint = _isAuthenticated ? _dynamicServerEndPoint : _initialServerEndPoint;

            if (iPEndpoint == null)
            {
                Console.WriteLine("ERROR: Endpoint is not initialized.");
                Environment.Exit(1);
                return;
            }

            // this class is used to store info about corrently sent message to possible later resend it
            UdpSentMessageInfo sentMessageInfo = new UdpSentMessageInfo(
                messageId: message.MessageId,
                payload: payload,
                targetEndPoint: iPEndpoint
            );
            // message id is added to the dictionary with messages pending to confirm
            _pendingConfirmationMessages[message.MessageId] = sentMessageInfo;

            // retry loop is started
            StartRetryLoop(message.MessageId, sentMessageInfo);
        }

        private void StartRetryLoop(int messageId, UdpSentMessageInfo sentMessageInfo)
        {
            // Check if the socket is initialized
            Task.Run(async () =>
            {
                while (sentMessageInfo.RetryCount < _maxRetries)
                {
                    await Task.Delay(_timeoutMs);

                    // if id is not in pending list, message was already confirmed
                    if (!_pendingConfirmationMessages.ContainsKey(messageId))
                    {
                        break;
                    }

                    Console.Error.WriteLine($"DEBUG: Retrying message ID {messageId}, attempt {sentMessageInfo.RetryCount + 1}");
                    try
                    {
                        // resend message to the same endpoint
                        _socket?.SendTo(sentMessageInfo.Payload, SocketFlags.None, sentMessageInfo.TargetEndPoint);
                        sentMessageInfo.RetryCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR during message retry: {ex.Message}");
                        break;
                    }
                }
                // if message was not confirmed after max retries, remove it from pending list
                // and disconnect with error
                if (_pendingConfirmationMessages.ContainsKey(messageId))
                {
                    Console.WriteLine($"ERROR: Retries exhausted for message ID {messageId}");
                    _pendingConfirmationMessages.Remove(messageId);
                    await DisconnectAsync("Failed to receive confirmation for AUTH message.", 1);
                }
            });
        }

        private bool SendUdpPayloadToServer(byte[] payload, bool isConfirmation = false)
        {
            if (_socket == null)
            {
                Console.WriteLine("ERROR: Socket is not initialized.");
                return false;
            }

            if (_initialServerEndPoint == null)
            {
                Console.WriteLine("ERROR: Active server endpoint is not set.");
                return false;
            }

            try
            {
                int bytesSent;
                // try to send payload to the server
                // if authenticated, send to dynamic server endpoint
                // otherwise send to initial server endpoint
                if (_isAuthenticated)
                {
                    if (_dynamicServerEndPoint != null)
                    {
                        bytesSent = _socket.SendTo(payload, SocketFlags.None, _dynamicServerEndPoint);
                    }
                    else
                    {
                        throw new InvalidOperationException("Dynamic server endpoint is not set.");
                    }
                }
                else
                {
                    bytesSent = _socket.SendTo(payload, SocketFlags.None, _initialServerEndPoint);
                }

                if (bytesSent != payload.Length)
                {
                    Console.WriteLine($"ERROR: Sent {bytesSent} bytes, but expected to send {payload.Length} bytes.");
                    return false;
                }

                return true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"ERROR: Socket error while sending payload: {ex.Message} (Code: {ex.SocketErrorCode})");
                return false;
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("ERROR: Socket was disposed while sending payload.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Unexpected error while sending payload: {ex.Message}");
                return false;
            }
        }
        // this method is used to track incoming messages
        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            if (_socket == null)
            {
                Console.WriteLine("ERROR: Socket is not initialized.");
                await DisconnectAsync("Socket missing in ReceiveLoop.", 1);
                return;
            }

            byte[] buffer = new byte[65507]; // Max UDP payload size

            try
            {
                // Loop to receive messages
                while (!token.IsCancellationRequested && _currentState != ClientState.End)
                {
                    ushort messageId = 0;
                    try
                    {
                        // receive on any interface
                        EndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
                        var bufferSegment = new ArraySegment<byte>(buffer);
                        // Receive the message
                        SocketReceiveFromResult result = await _socket.ReceiveFromAsync(bufferSegment, SocketFlags.None, senderEndPoint, token);
                        // read bytes
                        var receivedBytes = bufferSegment.Slice(0, result.ReceivedBytes);
                        // get message type and message id
                        byte firstByte = receivedBytes[0];
                        messageId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(receivedBytes.Slice(1, 2).ToArray(), 0));

                        // check if message is already confirmed
                        if(firstByte != 0) {
                            ConfirmMessage confirmation = new ConfirmMessage(messageId);
                            SendUdpPayloadToServer(confirmation.GetBytesForUdpPacket(), true);
                        }
                        // if message is not a confirmation, check if it was already confirmed, and if yes
                        // ignore it
                        if(firstByte != 1) {
                            if (alreadyConfirmedIds.Contains(messageId))
                            {
                                Console.Error.WriteLine($"DEBUG: Ignoring incoming message with ID {messageId} as it has already been confirmed.");
                                continue;
                            }
                            
                        }

                        // get sender endpoint
                        var senderRemoteEndPoint = (IPEndPoint)result.RemoteEndPoint;
                        // parse message
                        Message? parsedMessage = UdpMessageParser.ParseIncommingUdpMessage(receivedBytes.ToArray());
                        if (parsedMessage == null)
                        {
                            
                        }
                        // reply message is used to confirm auth message or join message
                        if (parsedMessage is ReplyAuthMessage replyMessage)
                        {
                            Console.WriteLine(replyMessage.ToString());
                            
                            if (replyMessage.IsSuccess)
                            {
                                CurrentState = ClientState.Open;
                            }
                            // in any case, whater success or not, send endpoint with dynamic port
                            if (!_isAuthenticated)
                            {
                                _isAuthenticated = true;
                                _dynamicServerEndPoint = senderRemoteEndPoint;
                            }
                            // delete message from confirm pending list
                            if (_pendingConfirmationMessages.ContainsKey(parsedMessage.MessageId))
                            {
                                _pendingConfirmationMessages.Remove(parsedMessage.MessageId);
                            }
                        }
                        if(parsedMessage is ConfirmMessage confirmMessage) {
                            if(confirmMessage.typeOfMessageWasConfirmed == MessageType.BYE) {
                                await SendByeMessageAsync();
                                await DisconnectAsync("Bye message sent.", 0);
                            }
                        } 
                        // write incoming msg message to console
                        if (parsedMessage is MsgMessage msgMessage)
                        {
                            Console.WriteLine(msgMessage.ToString());
                        }
                        // write incoming bye message to console
                        if (parsedMessage is ErrMessage errMessage)
                        {
                            Console.WriteLine(errMessage.ToString());
                            await DisconnectAsync("Server error: " + errMessage.MessageContent, 1);
                        }
                        // when bye message is received, disconnect with exit code 0
                        if(parsedMessage is ByeMessage byeMessage)
                        {
                            Console.WriteLine(byeMessage.ToString());
                            await DisconnectAsync("Server closed connection.", 0);
                        }
                    }
                    catch(ArgumentException ex) {
                        if(ex.Message == "malformed message") {
                            Console.WriteLine($"ERROR: Malformed message is received");
                            // send err message to server and disconnect
                            ErrMessage error = new ErrMessage(DisplayName, "Malformed message", messageId);
                            SendUdpPayloadToServer(error.GetBytesForUdpPacket());
                            await DisconnectAsync("Malformed message received.", 1);
                        }
                        
                    }
                    // Exceptions handling
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("ERROR: Receive loop canceled.");
                        break;
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"ERROR: Socket error while receiving: {ex.Message} (Code: {ex.SocketErrorCode})");
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        Console.WriteLine("ERROR: Socket disposed during receive.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR: Unexpected error in receive loop: {ex.Message}");
                        break;
                    }
                }
            }
            finally
            {
                Console.WriteLine("DEBUG: Receive loop terminated.");
                if (_currentState != ClientState.End)
                {
                    await DisconnectAsync("Receive loop terminated unexpectedly.");
                }
            }
        }
        // this method is used to process messages
        private async Task ProcessMessageAsync(Message message)
        {
            switch (message)
            {
                // sent auth messages in start or auth state
                case AuthMessage authMessage when CurrentState == ClientState.Auth || CurrentState == ClientState.Start:
                    
                    if (authMessage.DisplayName != null)
                    {
                        // set displayname for client based on user input in auth message
                        DisplayName = authMessage.DisplayName;
                    }
                    else
                    {
                        Console.WriteLine("ERROR: DisplayName cannot be null.");
                    }
                    byte[] payload = authMessage.GetBytesForUdpPacket();
                    
                    if (!SendUdpPayloadToServer(payload))
                    {
                        break;
                    }

                    await Task.Run(() => WaitConfirm(authMessage, payload));
                    break;
                // bye message can be sent in any state
                case ByeMessage byeMessage:
                    byte[] byePayload = byeMessage.GetBytesForUdpPacket();
                    SendUdpPayloadToServer(byePayload);
                    await Task.Run(() => WaitConfirm(byeMessage, byePayload));
                    await DisconnectAsync("Bye message sent.", 0);
                    break;
                // msg message can be sent only in open state
                case MsgMessage msgMessage when CurrentState == ClientState.Open:
                    byte[] msgPayload = msgMessage.GetBytesForUdpPacket();
                    SendUdpPayloadToServer(msgPayload);
                    await Task.Run(() => WaitConfirm(msgMessage, msgPayload));
                    break;
                // join message can be sent only in open state
                case JoinMessage joinMessage when CurrentState == ClientState.Open:
                    byte[] joinPayload = joinMessage.GetBytesForUdpPacket();
                    SendUdpPayloadToServer(joinPayload);
                    await Task.Run(() => WaitConfirm(joinMessage, joinPayload));
                    CurrentState = ClientState.Join;
                    break;
                // if message is not recognized, print error
                default:
                    Console.WriteLine("ERROR: Unsupported command in state " + CurrentState);
                    break;
            }
        }
        // handle crtl C (SIGINT)
        public void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            // send bye message and wait for confirmation
            _ = SendByeMessageAsync();
        }
        private async Task DisconnectAsync(string reason, int exitCode = 0)
        {
            bool alreadyEnded = false;

            lock (_stateLock)
            {
                if (_currentState == ClientState.End)
                {
                    alreadyEnded = true;
                }
                else
                {
                    Console.Error.WriteLine($"Disconnecting... Reason: {reason}");
                    _currentState = ClientState.End; // Set state to End
                }
            }

            if (alreadyEnded && _socket == null) return;

            if (!_cts.IsCancellationRequested)
            {
                try
                {
                    _cts.Cancel(); // Signal cancellation
                    Console.Error.WriteLine("DEBUG: Cancellation signaled to background tasks.");
                }
                catch (ObjectDisposedException)
                {
                    Console.Error.WriteLine("DEBUG: CancellationTokenSource already disposed during cancellation signal.");
                }
            }

            Socket? socketToClose = _socket;

            _socket = null;

            if (socketToClose != null)
            {
                Console.Error.WriteLine("DEBUG: Closing network socket...");
                try
                {
                    socketToClose.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException ex)
                {
                    Console.Error.WriteLine($"DEBUG: SocketException during socket shutdown (ignoring): {ex.SocketErrorCode} - {ex.Message}");
                }
                catch (ObjectDisposedException)
                {
                    Console.Error.WriteLine($"DEBUG: Socket was already disposed during shutdown attempt.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"DEBUG: Unexpected exception during socket shutdown (ignoring): {ex.Message}");
                }
                finally
                {
                    try
                    {
                        socketToClose.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"DEBUG: Exception during socket close (ignoring): {ex.Message}");
                    }
                }
            }

            Console.Error.WriteLine("Debug: Disconnected.");
            await Task.CompletedTask;
            Environment.Exit(exitCode);
        }
    }
}
