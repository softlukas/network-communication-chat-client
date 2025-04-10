using System;
using System.Threading.Tasks;

namespace Ipk25Chat
{
    // Message type enum
    public enum MessageType { AUTH, REPLY, JOIN, MSG, ERR, BYE, CONFIRM, PING }

    // Base abstract class for message
    public abstract class Message
    {
        public abstract MessageType Type { get; } 
        
        public abstract byte[] GetTcpPayload();

        public static async Task<Message?> CreateMessageFromUserInputAsync(TcpChatClient tcpChatClient)
        {
            // Read line async (non-blocking)
            string? userInput = await Task.Run(() => Console.ReadLine());

            // Handle empty or EOF input
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return null;
            }

            // Trim whitespace
            string trimmedInput = userInput.Trim();

            // Check for /auth command
            if (trimmedInput.StartsWith("/auth ", StringComparison.OrdinalIgnoreCase))
            {
                // Extract arguments string
                string argsString = trimmedInput.Substring("/auth ".Length);

                // Call static parser on AuthMessage class
                string[] parsedArgs = AuthMessage.ParseAuthMessageArgs(argsString, tcpChatClient);

                return new AuthMessage
                (
                    username: parsedArgs[0],
                    secret: parsedArgs[1],
                    displayName: parsedArgs[2]
                );
            }

            if (tcpChatClient.CurrentState == ClientState.Open)
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
                string[] parsedArgs = JoinMessage.ParseJoinMessageArgs(argsString, tcpChatClient);
                return new JoinMessage
                (
                    channelId: parsedArgs[0],
                    displayName: tcpChatClient.DisplayName
                );
                
            }
            return null;
        }
    } // End of Message class
}