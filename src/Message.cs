// In Message.cs
using System;
using System.Threading.Tasks;

namespace Ipk25Chat
{
    // Message type enum (as defined before)
    public enum MessageType { AUTH, REPLY, JOIN, MSG, ERR, BYE, CONFIRM, PING }

    // Base abstract class for message data containers.
    public abstract class Message
    {
        // Gets the specific logical type of this protocol message.
        public abstract MessageType Type { get; }

        // Async static factory method to read user input and parse message commands.
        // Returns a specific Message object (e.g., AuthMessage) if input is a valid command,
        // otherwise returns null (for non-message commands, plain text, errors, or EOF)
        public static async Task<Message?> CreateMessageFromUserInputAsync()
        {
            Console.Write("Enter command/message: "); // Prompt user (optional)
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
                return AuthMessage.TryParseArguments(argsString);
            }
            
            

            
            return null;
        }

        

    } // End of Message class
}