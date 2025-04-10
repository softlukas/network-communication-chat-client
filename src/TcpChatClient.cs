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
        
        private readonly IMessageParser _messageParser; // MessageParser interface

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
        
        public TcpChatClient(string server, ushort port, IMessageParser parser)
        {
            _messageParser = parser;
            Server = server;
            _port = port;
        }
        
        public async Task Start() {
            while(currentState != ClientState.End) {
                switch(currentState) {
                    case ClientState.Start:
                        // here send auth message

                        AuthMessage authMessage = (AuthMessage) await Message.CreateMessageFromUserInputAsync();
                        
                        Console.WriteLine(authMessage.ToString());
                        byte[] payload = authMessage.GetTcpPayload();
                        SnedPayload(payload);
                        //parsing args, create authMessage object
                        currentState = ClientState.Auth;
                        //currentState = ClientState.End;
                        break;
                }
                
            }
            
        }

        private async void SnedPayload(byte[] payload) {
            using (var client = new System.Net.Sockets.TcpClient())
            {
                try
                {
                    await client.ConnectAsync(_server, _port);
                    using (var networkStream = client.GetStream())
                    {
                        await networkStream.WriteAsync(payload, 0, payload.Length);
                        await networkStream.FlushAsync();
                        Console.WriteLine($"Sent payload: {BitConverter.ToString(payload)}");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error sending payload: {ex.Message}");
                }
            }
        }

        public override string ToString()
        {
            return $"TcpChatClient: Server={_server}, Port={_port}";
        }
    }
}
