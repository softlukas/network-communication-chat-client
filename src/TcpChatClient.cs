using System;
using System.Net;

namespace Ipk25Chat
{
    public class TcpChatClient
    {
        private IPAddress _server;

        private enum ClientState
        {
            // Initial state after program start, waiting for /auth.
            Start,

            // State after sending AUTH, waiting for REPLY. ('auth' node)
            Auth,

            // State after sending JOIN, waiting for REPLY. ('join' node)
            Join,

            // State after successful auth/join, ready for MSG. ('open' node)
            Chat,

            // Final disconnected state. ('end' node)
            End
        }

        private ClientState currentState = ClientState.Start;

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
                        var addresses = Dns.GetHostAddresses(value);
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
        private ushort _port;

        public TcpChatClient(string server, ushort port)
        {
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
