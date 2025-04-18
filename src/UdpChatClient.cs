using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Ipk25Chat {

    
public class UdpChatClient : ChatClient
{
    
    public static Dictionary<int, UdpSentMessageInfo> _pendingConfirmationMessages = new Dictionary<int, UdpSentMessageInfo>();
    public static HashSet<int> alreadyConfirmedIds = new HashSet<int>();

    
    private readonly ushort _timeoutMs; 
    private readonly byte _maxRetries;
    

    public string DisplayName { get; set; } // Display name of the client

    bool _isAuthenticated = false; // Flag to track authentication status


    
    private Socket? _socket;
    private IPEndPoint? _initialServerEndPoint; 
    private IPEndPoint? _dynamicServerEndPoint;  

   
    
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    
    

    private MessageType[] _messageRequiresConfirmation = { 
        MessageType.AUTH,
        MessageType.JOIN,
        MessageType.MSG,
        MessageType.BYE
    };


    UdpMessageParser _messageParser = new UdpMessageParser();

    // Message ID counter for sent messages
    private int _nextMessageId = -1;
    private readonly object _messageIdLock = new object(); // Lock for accessing _nextMessageId if needed later

    

    private readonly SemaphoreSlim _stateSemaphore = new SemaphoreSlim(1, 1);
    
    public UdpChatClient(string server, ushort port, ushort timeOutMs, byte maxRetries) : base(server, port) { 
        this._timeoutMs = timeOutMs;
        this._maxRetries = maxRetries;
        InitEndpoint(); 
    }


    private void InitEndpoint() {
        _initialServerEndPoint = new IPEndPoint(_server, _port);
    }

    
    private int GetNextMessageId()
    {
        lock (_messageIdLock)
        {
            _nextMessageId++;
            return _nextMessageId;
        }
    }

   
    private void InitializeSocket()
    {
        if (_socket != null)
        {
            Console.WriteLine("DEBUG: Socket is already initialized.");
            return;
        }

        try
        {
            // Create a new UDP socket
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 0);

            // 3. Bind the socket to the local endpoint
            _socket.Bind(localEndPoint);

            Console.WriteLine("DEBUG: Socket initialized successfully.");
        }
        catch (SocketException ex)
        {
            OutputError($"Failed to initialize socket: {ex.Message} (SocketErrorCode: {ex.SocketErrorCode})");
            _socket = null;
            _currentState = ClientState.End; // Transition to End state on failure
        }
        catch (Exception ex)
        {
            OutputError($"Unexpected error during socket initialization: {ex.Message}");
            _socket = null;
            _currentState = ClientState.End;
        }
    }
    /*
    public async Task Start()
    {
        // Set up console cancel event handler
        byte[] payload = new byte[60000];

        Message? message = null;

        // fsm implementation
        while (CurrentState != ClientState.End)
        {
            try
            {
                switch (CurrentState)
                {
                    case ClientState.Start:
                        await HandleStartStateAsync();
                        break;

                    case ClientState.Auth:
                        await HandleAuthStateAsync();
                        break;

                    case ClientState.Open:
                        await HandleOpenStateAsync();
                        break;

                    case ClientState.Join:
                        await HandleJoinStateAsync();
                        break;

                    case ClientState.End:
                        Console.Error.WriteLine("Debug: End state reached");
                        await DisconnectAsync("End state reached");
                        Environment.Exit(0);
                        break;
                }
            }
            // cath invalid input
            catch (ArgumentException ex)
            {
                await HandleArgumentExceptionAsync(ex.Message);
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
    /*
    private async HandleStartStateAsync()
    {
        // Initialize socket and bind
        InitializeSocket();
        Console.Error.WriteLine("Debug: clientsate: " + CurrentState.ToString());
        _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        // Start the receiver loop
        //_ = Task.Run(() => ReceiveLoopAsyncUdp(_cts.Token), _cts.Token);
         // Move to next state
        
        SentAuthMessage();
        
        CurrentState = ClientState.Auth;
        Thread.Sleep(100);
    }
    */

    
    public async Task RunAsync() {
        while(CurrentState != ClientState.End) {

            switch(CurrentState) {
                case ClientState.Start:
                    // Initialize socket and bind
                    InitializeSocket();
                    Console.Error.WriteLine("Debug: clientsate: " + CurrentState.ToString());
                    _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
                    
                    
                    SentAuthMessage();
                    
                   
                    Thread.Sleep(100);
                    break;

                case ClientState.Open:
                    //MsgMessage msgMessage = (MsgMessage) UdpMessageFormatter.CreateMessageFromUserInputAsync(GetNextMessageId());
                    SentMsgMessage();
                    break;

                case ClientState.Auth:
                    Console.Error.WriteLine("Debug: I am in auth state");
                    SentAuthMessage();
                    break;

                case ClientState.End:
                    // Cleanup and exit
                    //await DisconnectAsync("Client ended.");
                    break;

                default:
                    break;
            }

        }
    }
    

    private void SentMsgMessage() {
        try {
            MsgMessage msgMessage = (MsgMessage) UdpMessageFormatter.CreateMessageFromUserInputAsync(GetNextMessageId(), this);
            byte[] payload = msgMessage.GetBytesForUdpPacket();
            foreach(byte item in payload) {
                Console.Error.WriteLine("Debug: payload byte: " + item);
            }
            bool sent = SendUdpPayloadToServer(payload);
            WaitConfirm(msgMessage, payload);
        }
        catch(ArgumentNullException ex) {
            SendByeMessageAsync();
        }
        catch(ArgumentException ex) {
            Console.WriteLine("ERROR: " + ex.Message);
        }
        catch(Exception ex) {
            Console.WriteLine("ERROR: " + ex.Message);
        }
        
    }

    private async Task SendByeMessageAsync() {
        ByeMessage byeMessage = new ByeMessage(this.DisplayName, GetNextMessageId());
        byte[] payload = byeMessage.GetBytesForUdpPacket();
        SendUdpPayloadToServer(payload);
        WaitConfirm(byeMessage, payload);
    }

    private async Task SentAuthMessage() {
        try {
            //int nextMessageId = GetNextMessageId();
            AuthMessage authMessage = (AuthMessage) UdpMessageFormatter.CreateMessageFromUserInputAsync(GetNextMessageId(), this);
            
            if(authMessage == null) {
                Console.WriteLine("ERROR: Invalid command. You need to enter /auth command.");
                return;
            }

            switch(authMessage.Type) {
                case MessageType.AUTH:
                    break;
                case MessageType.BYE:
                    break;
                default:
                    Console.WriteLine("ERROR: Invalid command. You need to enter /auth command.");
                    return;
            }

            DisplayName = authMessage.DisplayName;
            
            //Console.Error.WriteLine("Message id: " + authMessage.MessageId);
            byte[] payload = authMessage.GetBytesForUdpPacket();


            bool sent = SendUdpPayloadToServer(payload);
            
            WaitConfirm(authMessage, payload);
            //CurrentState = ClientState.Auth;
        }
        catch(ArgumentNullException ex) {
            SendByeMessageAsync();
        }
        catch(ArgumentException ex) {
            Console.WriteLine("ERROR: " + ex.Message);
        }
        catch(Exception ex) {
            Console.WriteLine("ERROR: " + ex.Message);
        }
        
    }

    private void WaitConfirm(Message message, byte[] payload) {
        IPEndPoint iPEndpoint = null;
        if(_isAuthenticated) {
            iPEndpoint = _dynamicServerEndPoint;
        }
        else if(!_isAuthenticated) {
            iPEndpoint = _initialServerEndPoint;
        }
        UdpSentMessageInfo sentMessageInfo = new UdpSentMessageInfo
        (
            messageId: message.MessageId,
            payload: payload,
            targetEndPoint: iPEndpoint
        );
        _pendingConfirmationMessages[message.MessageId] = sentMessageInfo;
        
        // Start a timer to handle retransmissions if confirmation is not received
        StartRetryLoop(message.MessageId, sentMessageInfo);
    }

    private void StartRetryLoop(int messageId, UdpSentMessageInfo sentMessageInfo)
    {
        Console.WriteLine("Current messageId passed to the function:" + messageId);
        Task.Run(async () =>
        {
            while (sentMessageInfo.RetryCount < _maxRetries)
            {
                await Task.Delay(_timeoutMs);

                if (!_pendingConfirmationMessages.ContainsKey(messageId))
                {
                    // Confirmation received, exit the loop
                    Console.Error.WriteLine("Debug: receive confirmation of message: " + messageId);
                    break;
                }

                // Retry sending the message
                Console.WriteLine($"DEBUG: Retrying message ID {messageId}, attempt {sentMessageInfo.RetryCount + 1}");
                try
                {
                    _socket?.SendTo(sentMessageInfo.Payload, SocketFlags.None, sentMessageInfo.TargetEndPoint);
                    sentMessageInfo.RetryCount++;
                }
                catch (Exception ex)
                {
                    OutputError($"Error during message retry: {ex.Message}");
                    break;
                }
            }

            if (_pendingConfirmationMessages.ContainsKey(messageId))
            {
                // Retries exhausted, remove the message and handle failure
                Console.WriteLine($"DEBUG: Retries exhausted for message ID {messageId}");
                _pendingConfirmationMessages.Remove(messageId);
                await DisconnectAsync("Failed to receive confirmation for AUTH message.");
            }
        });
    }
     
    private bool SendUdpPayloadToServer(byte[] payload)
    {
        if (_socket == null)
        {
            OutputError("Cannot send payload: Socket is not initialized.");
            return false;
        }

        if (_initialServerEndPoint == null)
        {
            OutputError("Cannot send payload: Active server endpoint is not set.");
            return false;
        }

        try
        {

            ushort payloadMessageId = (ushort)payload[2];
            int bytesSent = 0;
            if(_isAuthenticated) {
                bytesSent = _socket.SendTo(payload, SocketFlags.None, _dynamicServerEndPoint);
                Console.Error.WriteLine("Debug: payload sent to dynamic server endpoint");
            }
            else {
                bytesSent = _socket.SendTo(payload, SocketFlags.None, _initialServerEndPoint);
            }
                
            if (bytesSent != payload.Length)
            {
                OutputError($"Failed to send all bytes ({bytesSent}/{payload.Length}) to {_dynamicServerEndPoint}.");
                return false;
            }

            Console.Error.WriteLine($"Message with id {payloadMessageId} was sent");
            return true;
        }
        catch (SocketException ex)
        {
            OutputError($"Socket error while sending payload: {ex.Message} (Code: {ex.SocketErrorCode})");
            return false;
        }
        catch (ObjectDisposedException)
        {
            OutputError("Cannot send payload: Socket was disposed.");
            return false;
        }
        catch (Exception ex)
        {
            OutputError($"Unexpected error while sending payload: {ex.Message}");
            return false;
        }
    }
    
    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        if (_socket == null)
        {
            OutputError("Receive loop cannot start: Socket is not initialized.");
            await DisconnectAsync("Socket missing in ReceiveLoop.");
            return;
        }

        byte[] buffer = new byte[65507]; // Max UDP payload size

        try
        {
            while (!token.IsCancellationRequested && _currentState != ClientState.End)
            {
                
                try
                {
                    EndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    var bufferSegment = new ArraySegment<byte>(buffer);
                    //Console.Error.WriteLine("Debug: Receive on endpoint: " + senderEndPoint.ToString());
                    SocketReceiveFromResult result = await _socket.ReceiveFromAsync(bufferSegment, SocketFlags.None, senderEndPoint, token);

                    var receivedBytes = bufferSegment.Slice(0, result.ReceivedBytes);
                    var senderRemoteEndPoint = (IPEndPoint)result.RemoteEndPoint;

                
                    Message parsedMessage = UdpMessageParser.ParseUdp(receivedBytes.ToArray());

                    if(parsedMessage is ReplyAuthMessage) {
                        ReplyAuthMessage replyMessage = (ReplyAuthMessage)parsedMessage;
                        if(replyMessage.IsSuccess) {
                            _isAuthenticated = true;
                            _dynamicServerEndPoint = senderRemoteEndPoint;

                            ConfirmMessage confirmMessage = new ConfirmMessage(parsedMessage.MessageId);
                            SendUdpPayloadToServer(confirmMessage.GetBytesForUdpPacket());
                            CurrentState = ClientState.Open;
                            
                            //Console.Error.WriteLine("DEBUG: Received REPLY from server. Authentication successful.");
                        } 
                        
                    }

                    // todo ostatne spravy
                    if(parsedMessage is PingMessage) {
                        Console.Error.WriteLine("Debug: dostal som ping");
                        ConfirmMessage confirmMessage = new ConfirmMessage(parsedMessage.MessageId);
                        SendUdpPayloadToServer(confirmMessage.GetBytesForUdpPacket());
                    }

                    if(parsedMessage is ConfirmMessage) {
                        if(parsedMessage != null) {
                            ConfirmMessage confirm = (ConfirmMessage)parsedMessage;
                            if(confirm.typeOfMessageWasConfirmed == MessageType.BYE) {
                                await DisconnectAsync("bye message was sent");
                                Environment.Exit(0);
                            }
                        }
                        // confirmed message is null and already has ben confirmed
                        else {
                            continue;
                        }
                    }

                    if(parsedMessage is ByeMessage) {
                        ConfirmMessage confirmMessage = new ConfirmMessage(parsedMessage.MessageId);
                        SendUdpPayloadToServer(confirmMessage.GetBytesForUdpPacket());
                        DisconnectAsync("Bye message received");
                        Environment.Exit(0);
                    }

                    if(parsedMessage is MsgMessage) {
                        ConfirmMessage confirmMessage = new ConfirmMessage(parsedMessage.MessageId);
                        SendUdpPayloadToServer(confirmMessage.GetBytesForUdpPacket());
                        MsgMessage msgMessage = (MsgMessage)parsedMessage;
                       Console.WriteLine(msgMessage.ToString());
                    }



                    
                    /*
                    if (message != null)
                    {
                        ProcessNetworkDatagram(message, messageId, senderRemoteEndPoint);
                    }
                    else if (receivedBytes.Count > 0)
                    {
                        OutputError($"Failed to parse UDP datagram from {senderRemoteEndPoint}. Size: {receivedBytes.Count} bytes.");
                    }
                    */
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("DEBUG: Receive loop canceled.");
                    break;
                }
                catch (SocketException ex)
                {
                    OutputError($"Socket error in receive loop: {ex.Message} (Code: {ex.SocketErrorCode})");
                    if (ex.SocketErrorCode == SocketError.ConnectionReset || ex.SocketErrorCode == SocketError.ConnectionAborted)
                    {
                        break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("DEBUG: Socket disposed during receive.");
                    break;
                }
                catch (Exception ex)
                {
                    OutputError($"Unexpected error in receive loop: {ex.Message}");
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
    
    private async Task ProcessMessageAsync(Message message)
    {
        switch (message)
        {
            case AuthMessage authMessage when nextState == ClientState.Auth || nextState == ClientState.Start:
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

            case MsgMessage msgMessage when nextState == ClientState.Open:
                await SendPayloadAsync(msgMessage.GetBytesInTcpGrammar());
                break;

            case JoinMessage joinMessage when nextState == ClientState.Open:
                await SendPayloadAsync(joinMessage.GetBytesInTcpGrammar());
                CurrentState = ClientState.Join;
                break;

            default:
                Console.WriteLine("ERROR: Unsupported command in state " + CurrentState);
                break;
        }

        await Task.Delay(100);
    }
    

    // todo implementovat
    public void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        SendByeMessageAsync();
    }


    
    
    private async Task DisconnectAsync(string reason)
    {
       
        bool alreadyEnded = false;
       
        lock(_stateLock) 
        {
            if (_currentState == ClientState.End)
            {
                alreadyEnded = true;
            }
            else
            {
                Console.WriteLine($"Disconnecting... Reason: {reason}");
                _currentState = ClientState.End; // Set state to End
            }
        }
       
        if (alreadyEnded && _socket == null) return;

        
        if (!_cts.IsCancellationRequested)
        {
            try
            {
                _cts.Cancel(); // Signal cancellation
                Console.WriteLine("DEBUG: Cancellation signaled to background tasks.");
            }
            catch (ObjectDisposedException)
            {
                // Ignore if CancellationTokenSource was already disposed
                Console.WriteLine("DEBUG: CancellationTokenSource already disposed during cancellation signal.");
            }
        }

        
        Socket? socketToClose = _socket;

        
        _socket = null;

        if (socketToClose != null)
        {
            Console.WriteLine("DEBUG: Closing network socket...");
            try
            {
                
                socketToClose.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException ex)
            {
                // Ignore common errors during shutdown (e.g., already closed)
                Console.WriteLine($"DEBUG: SocketException during socket shutdown (ignoring): {ex.SocketErrorCode} - {ex.Message}");
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed
                Console.WriteLine($"DEBUG: Socket was already disposed during shutdown attempt.");
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"DEBUG: Unexpected exception during socket shutdown (ignoring): {ex.Message}");
            }
            finally
            {
                
                try
                {
                    socketToClose.Close();
                }
                catch (Exception ex)
                {
                        Console.WriteLine($"DEBUG: Exception during socket close (ignoring): {ex.Message}");
                }
            }
        }

        
        Console.WriteLine("Disconnected.");
       
        await Task.CompletedTask;
    }

    private void OutputError(string messageContent)
    {
        
        if (string.IsNullOrEmpty(messageContent))
        {
            messageContent = "An unspecified error occurred.";
        }

        
        string formattedError = $"ERROR: {messageContent}";

        
        Console.WriteLine(formattedError);
    }
   
}
}