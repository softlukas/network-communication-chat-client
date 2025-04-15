using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ipk25Chat
{
    // Enum representing the different states of the client.
    public enum ClientState
    {
        // Initial state after program start, waiting for /auth.
        Start,

        // State after sending AUTH, waiting for REPLY. ('auth' node)
        Auth,

        // State after sending JOIN, waiting for REPLY. ('join' node)
        Join,

        // State after successful auth/join, ready for MSG. ('open' node)
        Open,

        // Final disconnected state. ('end' node)
        End
    }

    public class TcpChatClient
    {
        
        private IPAddress _server; // Server addresd (after translating from domain name)

        public string DisplayName { get; set; } // Display name of the client

        private readonly object _stateLock = new object(); // Lock for thread-safe state access
        
        // Property to get or set the current state of the client inside lock
        public ClientState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState;
                }
            }
            set
            {
                lock (_stateLock)
                {
                    Console.Error.WriteLine("Debug: Changing state to " + value.ToString());
                    _currentState = value;
                }
            }
        }
        private ClientState _currentState = ClientState.Start; // Initial state

        // Property to get or set the server address (use dns if required)
        public string Server 
        {
            get { return _server.ToString(); }
            private set { 
            
                // Try to parse the value as an IP address
                try {
                    _server = IPAddress.Parse(value);
                }
                catch (FormatException)
                {
                    // Try to resolve the domain name to an IP address
                    try
                    {
                        var addresses = Dns.GetHostAddresses(value).Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).ToArray();
                        if (addresses.Length > 0)
                        {
                            _server = addresses[0];
                                
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Invalid server address provided.");
                        Environment.Exit(1);
                    }
                }
                
            }
        }
        private ushort _port; // Port number of the server
        private TcpClient? _client; // TcpClient instance for network communication                                                                          
        private NetworkStream? _stream; // NetworkStream for reading/writing data

        // CancellationTokenSource for managing cancellation
        private readonly CancellationTokenSource _cts = new CancellationTokenSource(); 

        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        
        public TcpChatClient(string server, ushort port)
        {
            Server = server;
            _port = port;
        }
        
        public async Task Start() {

            byte[] payload = new byte[1000];
            while(CurrentState != ClientState.End) {
                Console.Error.WriteLine("Debug: Current state: " + CurrentState.ToString());

                switch(CurrentState) {
                    
                    case ClientState.Start:
                        // here send auth message
                        ConnectAsync();
                        Console.Error.WriteLine("Debug: Connected succesfully");
                        //parsing args, create authMessage object
                        CurrentState = (ClientState.Auth);
                        
                        break;

                    case ClientState.Auth:
                        
                        try {
                            Console.Error.WriteLine("Debug: Enter auth message");
                            //Console.Error.WriteLine("Debug: Enter auth message");
                            AuthMessage authMessage = (AuthMessage) await Message.CreateMessageFromUserInputAsync(this);
                            
                            ////Console.Error.WriteLine("Debug: " + authMessage.ToString());
                            
                            payload = authMessage.GetTcpPayload();
                            SendPayloadAsync(payload);
                            Thread.Sleep(1000);
                        }
                        catch(Exception ex) {
                            Console.Error.WriteLine($"Error: {ex}");
                            await DisconnectAsync("Auth failed.");
                            Environment.Exit(1);
                        }
                        finally {
                            // Perform any necessary cleanup here
                        }
                        break;
                        
                    case ClientState.Open:
                        
                        Message? message = await Message.CreateMessageFromUserInputAsync(this);

                        if(message is MsgMessage) {
                            MsgMessage msgMessage = (MsgMessage) message;
                            payload = msgMessage.GetTcpPayload();
                            SendPayloadAsync(payload);
                            break;
                        }
                        if(message is JoinMessage) {
                            JoinMessage joinMessage = (JoinMessage) message;
                            payload = joinMessage.GetTcpPayload();
                            SendPayloadAsync(payload);
                            CurrentState = ClientState.Join;
                            break;
                        }
                        if(message is ByeMessage) {
                            ByeMessage byeMessage = (ByeMessage) message;
                            payload = byeMessage.GetTcpPayload();
                            SendPayloadAsync(payload);
                            CurrentState = ClientState.End;
                            break;
                        }
                        
                        break;
                }
                
            }
            
        }

    private async Task<bool> SendPayloadAsync(byte[] payload)
    {
        // 1. Check if the stream exists and is writable
        //    We use the class member _stream established in ConnectAsync
        if (_stream == null || !_stream.CanWrite)
        {
            //Console.Error.WriteLine("Cannot send payload: Not connected or stream is not writable.");
            // If we can't write, assume the connection is unusable
            await DisconnectAsync("Send failed - stream error.");
            return false;
        }

        // 2. Check if payload is valid
        if (payload == null || payload.Length == 0) {
            Console.Error.WriteLine("Cannot send null or empty payload.");
            return false; // Don't treat as fatal error, just refuse to send
        }

        try
        {
            // 3. Write the byte array to the network stream asynchronously
            //    Pass the cancellation token to allow cancellation of the write operation
            await _stream.WriteAsync(payload, 0, payload.Length, _cts.Token);

            // 4. Flush the stream to ensure data is sent immediately (recommended)
            await _stream.FlushAsync(_cts.Token);

            // Console.WriteLine($"DEBUG: Sent {payload.Length} bytes."); // Optional debug output
            return true; // Sending was successful
        }
        // 5. Catch specific exceptions related to network I/O or cancellation
        catch (OperationCanceledException)
        {
            // The operation was cancelled, likely during shutdown
            //Console.Error.WriteLine("Debug: Sending payload cancelled.");
            return false;
        }
        catch (IOException ioEx)
        {
            // Network error during write (e.g., connection closed by peer)
            Console.Error.WriteLine($"Network write error: {ioEx.Message}");
            await DisconnectAsync($"Send failed (IO): {ioEx.Message}"); // Disconnect on failure
            return false;
        }
        catch (ObjectDisposedException)
        {
            // Stream or client was already disposed
            Console.Error.WriteLine("Cannot send payload: Connection already closed.");
            // Ensure state is End, DisconnectAsync might have been called already
            CurrentState = (ClientState.End);
            return false;
        }
        catch (Exception ex) // Catch any other unexpected errors
        {
            Console.Error.WriteLine($"Unexpected error sending payload: {ex.Message}");
            await DisconnectAsync($"Unexpected send error: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ConnectAsync()
    {
        // If already connected, do nothing and return success.
        if (_client != null && _client.Connected) return true;

        Console.Error.WriteLine($"Debug: Connecting to {_server}:{_port} via TCP...");

        var ipEndPoint = new IPEndPoint(_server, _port);

        try
        {
            // Create a new TcpClient instance.
            _client = new TcpClient();

            // Asynchronously connect to the server endpoint, passing the cancellation token.
            await _client.ConnectAsync(ipEndPoint, _cts.Token);

            // Get the network stream for reading and writing.
            _stream = _client.GetStream(); // Store the stream in the class member field

            ////Console.Error.WriteLine("Debug: Connected successfully.");

            // ---> START THE RECEIVING LOOP IMMEDIATELY AFTER CONNECTING <---
            // Start ReceiveLoopAsync on a background thread (fire-and-forget). Pass the token.
            // The '_' discards the returned Task as we don't need to await the whole loop here.
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

            // Return true indicating connection success.
            
            return true;
        }
        catch (OperationCanceledException)
        {
            // Catch cancellation specifically
            Console.Error.WriteLine("Connection attempt cancelled.");
            // DisconnectAsync should ideally handle cancellation token source disposal
            await DisconnectAsync("Connection cancelled.");
            return false; // Connection failed due to cancellation
        }
        catch (SocketException sockEx)
        {
            // Catch specific network connection errors
            Console.Error.WriteLine($"Connection failed: {sockEx.Message} (SocketErrorCode: {sockEx.SocketErrorCode})");
            await DisconnectAsync($"Connection failed: {sockEx.Message}"); // Clean up and set state to End
            return false; // Connection failed
        }
        catch (Exception ex)
        {
            // Catch any other unexpected errors during connection
            Console.Error.WriteLine($"Connection failed: {ex.Message}");
            await DisconnectAsync($"Connection failed: {ex.Message}"); // Clean up and set state to End
            return false; // Connection failed
        }
    }

       


    private async Task DisconnectAsync(string reason)
    {
        // Prevent running multiple times if already disconnected and cleaned up
        if (CurrentState == ClientState.End && _client == null && _stream == null)
        {
            // Console.WriteLine($"DEBUG: Already disconnected. Reason: {reason}"); // Optional debug
            return;
        }

    // Check state *before* acquiring semaphore maybe?
        // Or acquire semaphore first thing. Let's acquire first.

        
        // Prevent running multiple times
        if (CurrentState == ClientState.End && _client == null && _stream == null)
        {
            return; // Already fully disconnected
        }

        // Set state to End immediately to prevent further actions
        if (CurrentState != ClientState.End)
        {
            Console.WriteLine($"Disconnecting... Reason: {reason}");
            CurrentState = ClientState.End; // Protected write
        }
        
        
        // Signal cancellation to any ongoing async operations (like ReceiveLoopAsync)
        // Check if cancellation hasn't been requested already and CTS is not disposed
        if (!_cts.IsCancellationRequested)
        {
            try
            {
                _cts.Cancel(); // Signal cancellation
                //Console.Error.WriteLine("Debug: Cancellation signaled.");
            }
            catch (ObjectDisposedException)
            {
                // Ignore if CancellationTokenSource was already disposed
                //Console.Error.WriteLine("Debug: CancellationTokenSource already disposed during cancellation signal.");
            }
        }

        // Use temporary variables to hold resources for safe disposal
        // This helps if DisconnectAsync is somehow called concurrently (though unlikely)
        NetworkStream? streamToClose = _stream;
        TcpClient? clientToClose = _client;

        // Null out class members immediately to prevent further use
        _stream = null;
        _client = null;

        //Console.Error.WriteLine("Debug: Closing network resources...");

        // Close/Dispose the network stream (this often closes the underlying TcpClient too)
        try
        {
            streamToClose?.Close(); // Close() calls Dispose() internally
            // Alternatively: streamToClose?.Dispose();
        }
        catch (Exception ex)
        {
            // Log errors during close but don't stop the disconnect process
            Console.Error.WriteLine($"DEBUG: Exception during NetworkStream close (ignoring): {ex.Message}");
        }

        // Close/Dispose the TcpClient explicitly (might be redundant, but safe)
        try
        {
            clientToClose?.Close(); // Close() calls Dispose() internally
            // Alternatively: clientToClose?.Dispose();
        }
        catch (Exception ex)
        {
            // Log errors during close but don't stop the disconnect process
            Console.Error.WriteLine($"DEBUG: Exception during TcpClient close (ignoring): {ex.Message}");
        }

        ////Console.Error.WriteLine("Debug: Disconnected.");

        // Return a completed task to satisfy the 'async Task' signature.
        // No real async work needed after cleanup in this version.
        await Task.CompletedTask;
    }

    public override string ToString()
    {
        return $"TcpChatClient: Server={_server}, Port={_port}";
    }



    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        NetworkStream? stream = this._stream; // Use local variable
        if (stream == null) return;

        byte[] buffer = new byte[4096];
        

        //Console.Error.WriteLine("Debug: Receive loop started (integrated buffer processing).");
        try
        {
            while (!token.IsCancellationRequested && CurrentState != ClientState.End && stream.CanRead)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                if (token.IsCancellationRequested) break;
                if (bytesRead == 0) 
                { 
                    ////Console.Error.WriteLine("Debug: Server closed connection."); 
                    break; 
                }

                string receivedChunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                _receiveBuffer.Append(receivedChunk); // Append new data to the buffer

                // --- Integrated message processing ---
                string currentBufferContent = _receiveBuffer.ToString();
                int messageEndPos;
                string terminator = "\r\n";

                // Process all complete messages found in the buffer
                while ((messageEndPos = currentBufferContent.IndexOf(terminator)) != -1)
                {
                    if (CurrentState == ClientState.End || token.IsCancellationRequested) break;

                    // Extract the complete message string
                    string rawMessage = currentBufferContent.Substring(0, messageEndPos + terminator.Length);
                    // Remove the processed message from the beginning of our temporary copy
                    currentBufferContent = currentBufferContent.Substring(messageEndPos + terminator.Length);

                    // Console.WriteLine($"DEBUG: Extracted raw message -> {rawMessage.TrimEnd()}"); // Optional debug

                    // Try parsing the extracted message

                    //write output
                    TcpMessageParser.WriteParsedTcpIncomingMessage(rawMessage, tcpChatClient: this);

                }

                // Update the class buffer with the remaining unprocessed data
                _receiveBuffer.Clear();
                _receiveBuffer.Append(currentBufferContent);

            } // End outer while loop
        }
        // ... (Catch blocks as before) ...
        catch (OperationCanceledException) 
        { 
            ////Console.Error.WriteLine("Debug: Receive loop cancelled."); 
        }
        catch (Exception ex) { if (!token.IsCancellationRequested) Console.Error.WriteLine($"Receive loop error: {ex}"); }
        finally
        {
            //Console.Error.WriteLine("Debug: Receive loop finished.");
            if (!token.IsCancellationRequested && CurrentState != ClientState.End)
            {
                await DisconnectAsync("Receive loop terminated.");
            }
        }
    }

    public void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        ByeMessage byeMessage = new ByeMessage(this.DisplayName);
        SendPayloadAsync(byeMessage.GetBytesInTcpGrammar());
        CurrentState = ClientState.End;
    }



    }
}