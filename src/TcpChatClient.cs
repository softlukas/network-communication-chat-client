using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ipk25Chat
{
    public class TcpChatClient : ChatClient
    {
        // TCP client and stream for network communication
        private TcpClient? _client;
        // NetworkStream for reading/writing data
        private NetworkStream? _stream;
        // Cancellation token source for managing cancellation
        private readonly CancellationTokenSource _cts = new CancellationTokenSource(); 
        // StringBuilder for buffering incoming messages
        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        // base constructor
        public TcpChatClient(string server, ushort port) : base(server, port) { }

        // start point for the client
        public async Task Start()
        {
            // Set up console cancel event handler
            byte[] payload = new byte[60000];

            Message? message = null;

            bool connected = await ConnectAsync();
            if(!connected)
            {
                Console.WriteLine("ERROR: Connection failed.");
                Environment.Exit(1);
            }

            Console.Error.WriteLine("Debug: Connected successfully");

            // fsm implementation
            while (CurrentState != ClientState.End)
            {

                
                try
                {
                    // get user input and create message
                    message = await TcpMessageParser.CreateMessageFromUserInputAsync(this);

                    if(message == null)
                    {
                        Console.WriteLine("ERROR: Undefined command. Use /help");
                        continue;
                    }
                    // process message based on current state
                    await ProcessMessageAsync(message);

                    
                }
                // ignore multiple auth
                catch(InvalidOperationException ex){
                    if(ex.Message == "multiple auth") {
                        continue;
                    }
                    
                }
                
                // handle bye message
                catch(ArgumentNullException ex)
                {
                    var byeMessage = new ByeMessage(this.DisplayName);
                    SendPayloadAsync(byeMessage.GetBytesInTcpGrammar());
                    await DisconnectAsync("Bye message received.");
                    Environment.Exit(0);
                }
                // in rename and help command no message is created
                catch(ArgumentException ex)
                {
                    if(ex.Message == "rename" || ex.Message == "help") {

                        continue;
                    }
                    // handle other argument exceptions
                    else {
                        await HandleArgumentExceptionAsync(ex.Message);
                    }
                
                }
                // undifined exception
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex}");
                    await DisconnectAsync("Auth failed.");
                    Environment.Exit(1);
                }
            }

            Console.Error.WriteLine("Debug: End state reached");
            await DisconnectAsync("End state reached");
            Environment.Exit(0);
        }

       

        

        private async Task ProcessMessageAsync(Message message)
        {
            Console.Error.WriteLine("Debug: Processing message in state " + CurrentState);
            switch (message)
            {
                case AuthMessage authMessage when CurrentState != ClientState.Start || CurrentState != ClientState.Auth:
                    await SendPayloadAsync(authMessage.GetBytesInTcpGrammar());
                    CurrentState = ClientState.Auth;
                    break;

                case ByeMessage byeMessage:
                    await SendPayloadAsync(byeMessage.GetBytesInTcpGrammar());
                    CurrentState = ClientState.End;
                    break;

                case ErrMessage errMessage:
                    await SendPayloadAsync(errMessage.GetBytesInTcpGrammar());
                    CurrentState = ClientState.End;
                    break;

                case MsgMessage msgMessage when CurrentState == ClientState.Open:
                    await SendPayloadAsync(msgMessage.GetBytesInTcpGrammar());
                    break;

                case JoinMessage joinMessage when CurrentState == ClientState.Open:
                    await SendPayloadAsync(joinMessage.GetBytesInTcpGrammar());
                    CurrentState = ClientState.Join;
                    break;

                default:
                    Console.WriteLine("ERROR: Unsupported command in state " + CurrentState);
                    break;
            }

            
        }

        private async Task HandleArgumentExceptionAsync(string message)
        {
            Console.WriteLine(message);
        }

        private async Task<bool> SendPayloadAsync(byte[] payload)
        {
            if (_client == null || !_client.Connected || _stream == null || !_stream.CanWrite)
            {
                Console.Error.WriteLine("Send failed - client not connected or stream error.");
                await DisconnectAsync("Send failed - client not connected or stream error.");
                return false;
            }

            if (payload == null || payload.Length == 0)
            {
                Console.Error.WriteLine("Cannot send null or empty payload.");
                return false;
            }

            try
            {
                await _stream.WriteAsync(payload, 0, payload.Length, _cts.Token);
                await _stream.FlushAsync(_cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (IOException ioEx)
            {
                Console.Error.WriteLine($"Network write error: {ioEx.Message}");
                await DisconnectAsync($"Send failed (IO): {ioEx.Message}");
                return false;
            }
            catch (ObjectDisposedException)
            {
                Console.Error.WriteLine("Cannot send payload: Connection already closed.");
                CurrentState = ClientState.End;
                return false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error sending payload: {ex.Message}");
                await DisconnectAsync($"Unexpected send error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ConnectAsync() {
            if (_client != null && _client.Connected)
            {
                Console.Error.WriteLine("Debug: Already connected.");
                return true;
            }
            if (_client != null && _client.Connected) return true;

            Console.Error.WriteLine($"Debug: Connecting to {_server}:{_port} via TCP...");
            var ipEndPoint = new IPEndPoint(_server, _port);

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ipEndPoint, _cts.Token);
                _stream = _client.GetStream();
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Connection attempt cancelled.");
                await DisconnectAsync("Connection cancelled.");
                return false;
            }
            catch (SocketException sockEx)
            {
                Console.Error.WriteLine($"Connection failed: {sockEx.Message} (SocketErrorCode: {sockEx.SocketErrorCode})");
                await DisconnectAsync($"Connection failed: {sockEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Connection failed: {ex.Message}");
                await DisconnectAsync($"Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync(string reason)
        {
            if (CurrentState == ClientState.End && _client == null && _stream == null)
            {
                return;
            }

            if (CurrentState != ClientState.End)
            {
                Console.Error.WriteLine($"Disconnecting... Reason: {reason}");
                CurrentState = ClientState.End;
            }

            if (!_cts.IsCancellationRequested)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

            NetworkStream? streamToClose = _stream;
            TcpClient? clientToClose = _client;
            _stream = null;
            _client = null;

            try
            {
                streamToClose?.Close();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"DEBUG: Exception during NetworkStream close (ignoring): {ex.Message}");
            }

            try
            {
                clientToClose?.Close();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"DEBUG: Exception during TcpClient close (ignoring): {ex.Message}");
            }

            await Task.CompletedTask;
        }

        public override string ToString()
        {
            return $"TcpChatClient: Server={_server}, Port={_port}";
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            NetworkStream? stream = this._stream;
            if (stream == null) return;

            byte[] buffer = new byte[4096];

            try
            {
                // Loop to read incoming data
                while (!token.IsCancellationRequested && CurrentState != ClientState.End && stream.CanRead)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (token.IsCancellationRequested) break;
                    if (bytesRead == 0) 
                    { 
                        break; 
                    }

                    string receivedChunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    _receiveBuffer.Append(receivedChunk);
                    string currentBufferContent = _receiveBuffer.ToString();
                    int messageEndPos;
                    string terminator = "\r\n";

                    while ((messageEndPos = currentBufferContent.IndexOf(terminator)) != -1)
                    {
                        if (CurrentState == ClientState.End || token.IsCancellationRequested) break;

                        string rawMessage = currentBufferContent.Substring(0, messageEndPos + terminator.Length);
                        currentBufferContent = currentBufferContent.Substring(messageEndPos + terminator.Length);
                        var message = TcpMessageParser.WriteParsedTcpIncomingMessage(rawMessage, tcpChatClient: this);

                        if(message == null) {
                            // malformed message
                            Console.WriteLine("Malformed message received.");
                            ErrMessage errMessage = new ErrMessage(this.DisplayName, "Malformed message received.");
                            await SendPayloadAsync(errMessage.GetBytesInTcpGrammar());
                            await DisconnectAsync("Malformed message received");
                            Environment.Exit(1);
                        }

                        if (message is ByeMessage)
                        {
                            Console.Error.WriteLine("Bye message received, disconnecting...");
                            await DisconnectAsync("Bye message received.");
                            Environment.Exit(0);
                            CurrentState = ClientState.End;
                        }
                    }

                    _receiveBuffer.Clear();
                    _receiveBuffer.Append(currentBufferContent);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) 
            { 
                if (!token.IsCancellationRequested) 
                    Console.Error.WriteLine($"Receive loop error: {ex}"); 
            }
            finally
            {
                if (!token.IsCancellationRequested && CurrentState != ClientState.End)
                {
                    await DisconnectAsync("Receive loop terminated.");
                }
            }
        }
        // handle sigint
        public void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            var byeMessage = new ByeMessage(this.DisplayName);
            SendPayloadAsync(byeMessage.GetBytesInTcpGrammar());
            DisconnectAsync("Console cancel key press.");
            Environment.Exit(0);
            CurrentState = ClientState.End;
           
        }
    }
}
