using System;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace Ipk25Chat {


    public enum ClientState
    {
        Start,
        Auth,
        Join,
        Open,
        End
    }

    public class ChatClient {
       
        protected IPAddress _server; // Server address (after translating from domain name)

        public string DisplayName { get; set; } // Display name of the client

        protected readonly object _stateLock = new object(); // Lock for thread-safe state access
        
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
        protected ClientState _currentState = ClientState.Start; // Initial state

        // Property to get or set the server address (use dns if required)
        public string Server 
        {
            get { return _server.ToString(); }
            protected set { 
                _server = NetworkUtils.ResolveIpAddressFromDns(value);
            }
        }
        protected ushort _port; // Port number of the server

        public ChatClient(string server, ushort port) {
            Server = server;
            _port = port;
        }

    }


}