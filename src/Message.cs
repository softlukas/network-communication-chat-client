using System;
using System.Threading.Tasks;
using System.Text;

namespace Ipk25Chat
{
    // Message type enum
    public enum MessageType : byte // Explicitly use byte as underlying type
    {
        CONFIRM = 0x00,
        REPLY   = 0x01,
        AUTH    = 0x02,
        JOIN    = 0x03,
        MSG     = 0x04,
        PING    = 0xFD,
        ERR     = 0xFE, 
        BYE     = 0xFF 
    }

    // Base abstract class for message
    public abstract class Message
    {
        public abstract MessageType Type { get; }

        
        // used only if udp and no replymessage is
        public int MessageId {get; private set; } 

        public Message() {
            // Default constructor
        }
        public Message(int messageId) {
            // Constructor with message ID using in UDP
            MessageId = messageId;
        }
        
        public virtual byte[] GetBytesInTcpGrammar() {
            // Default implementation for TCP payload
            Console.Error.WriteLine("GetTcpPayload must call in derived class.");
            return null;
        }

        public virtual byte[] GetBytesForUdpPacket() {
            // Default implementation for UDP packet
            Console.Error.WriteLine("GetBytesForUdpPacket must call in derived class.");
            return null;
        }
        protected void WriteNullTerminated(BinaryWriter writer, string value)
        {
            if (value != null) // Write string bytes if not null
            {
                writer.Write(Encoding.ASCII.GetBytes(value));
            }
            writer.Write((byte)0x00); // Always write the null terminator
        }

        public static async Task<Message?> CreateMessageFromUserInputAsync(TcpChatClient tcpChatClient)
        {
            // Read line async (non-blocking)
            string? userInput = await Task.Run(() => Console.ReadLine());

            if(userInput == null)
            {
                throw new ArgumentNullException("User input is null");
            }

            // Check for EOF (Ctrl+D in Linux)
            ///if (userInput.Contains(EOF)) // ASCII code 4 represents EOF
            //{
                //Console.Error.WriteLine("EOF detected in user input.");
                //return null;
            //}

            // Handle empty or EOF input
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return null;
            }

            // Trim whitespace
            string trimmedInput = userInput.Trim();

            //Console.Error.WriteLine($"Debug: User input: {trimmedInput}");

            // Check for /auth command
            if (trimmedInput.StartsWith("/auth ", StringComparison.OrdinalIgnoreCase))
            {
                // Extract arguments string
                string argsString = trimmedInput.Substring("/auth ".Length);

                // Call static parser on AuthMessage class
                string[] parsedArgs = AuthMessage.ParseAuthMessageArgs(argsString);
                tcpChatClient.DisplayName = parsedArgs[2];
                AuthMessage? authMessage = null;
                try {
                    authMessage =  new AuthMessage
                    (
                    username: parsedArgs[0],
                    secret: parsedArgs[1],
                    displayName: parsedArgs[2]
                    );

                    if(tcpChatClient.CurrentState != ClientState.Join && tcpChatClient.CurrentState != ClientState.Open)
                    {
                       tcpChatClient.CurrentState = ClientState.Auth;
                    }
                    
                   
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException(ex.Message);
                }
                
                return authMessage;
            }

            if (trimmedInput.StartsWith("/rename ", StringComparison.OrdinalIgnoreCase))
            {
                string newDisplayName = trimmedInput.Substring("/rename ".Length);

                if (string.IsNullOrWhiteSpace(newDisplayName))
                {
                    Console.Error.WriteLine("Error: Display name cannot be empty.");
                    return null;
                }

                tcpChatClient.DisplayName = newDisplayName;
                throw new ArgumentException("rename");
            }
            
            if (tcpChatClient.CurrentState == ClientState.Open && trimmedInput != "/quit" && !trimmedInput.Contains("/join"))
            {
                
                Console.Error.WriteLine("Debug: Msg message object created");
                
                return new MsgMessage
                (
                    displayName: tcpChatClient.DisplayName, 
                    messageContent: trimmedInput
                );
                
            }
            if (trimmedInput.StartsWith("/join ", StringComparison.OrdinalIgnoreCase))
            {
                
                string argsString = trimmedInput.Substring("/join ".Length);

                // Parse arguments specifically for Join
                string[] parsedArgs = JoinMessage.ParseJoinMessageArgs(argsString);
                
                return new JoinMessage
                (
                    channelId: parsedArgs[0],
                    displayName: tcpChatClient.DisplayName
                );
                
            }

            

            if(trimmedInput == "/quit")
            {
                tcpChatClient.CurrentState = ClientState.End;
                return new ByeMessage(
                    displayName: tcpChatClient.DisplayName
                );
            }

            if(trimmedInput == "/help")
            {
                Console.WriteLine("Available commands: /auth, /rename, /join, /quit");
            }
           
            return null;
        }

        protected static bool IsValidUsername(string u) {
            if(!string.IsNullOrEmpty(u))
            {
                return u.Length <= 21 && u.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-');
            }
            return false;
        }

        protected static bool IsValidSecret(string s) {
            if(!string.IsNullOrEmpty(s))
            {
                return s.Length <= 128 && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-');
            }
            return false;
        }

        protected static bool IsValidDisplayName(string d) {
            if(!string.IsNullOrEmpty(d))
            {
                return d.Length <= 20 && d.All(c => c >= '!' && c <= '~');
            }
            return false;
        }

    } // End of Message class
}