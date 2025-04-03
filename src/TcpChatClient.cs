using System;
using System.Net;

namespace Ipk25Chat
{
    public class TcpChatClient
    {
        private IPAddress _server;

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

        public override string ToString()
        {
            return $"TcpChatClient: Server={_server}, Port={_port}";
        }
    }
}
