// V súbore AuthMessage.cs
using System;
using System.Linq;
using System.Text;

namespace Ipk25Chat
{
    // Represents the data required for an AUTH message.
    public class AuthMessage : Message
    {
        public override MessageType Type => MessageType.AUTH;
        public required string Username { get; init; }
        public required string DisplayName { get; init; }
        public required string Secret { get; init; }

        // --- Static factory method to parse arguments specific to AUTH ---
        // Takes the argument string (what comes after "/auth ")
        // Returns an AuthMessage object or null if parsing/validation fails.
        public static AuthMessage? TryParseArguments(string argsString)
        {
            if (string.IsNullOrWhiteSpace(argsString))
            {
                Console.Error.WriteLine("ERROR (Auth.TryParse): Arguments string is empty.");
                return null;
            }

            // Split arguments, expect 3 parts
            string[] parts = argsString.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 3)
            {
                string username = parts[0];
                string secret = parts[1];
                string displayName = parts[2];

                // --- Synchronous Validation (Example) ---
                if (!IsValidUsername(username) || !IsValidSecret(secret) || !IsValidDisplayName(displayName))
                {
                     Console.Error.WriteLine("ERROR (Auth.TryParse): Argument validation failed.");
                     return null;
                }

                // If arguments are OK, create and return the AuthMessage object
                return new AuthMessage
                {
                    Username = username,
                    DisplayName = displayName,
                    Secret = secret
                };
            }
            else
            {
                // Incorrect number of arguments
                 Console.Error.WriteLine("ERROR (Auth.TryParse): Invalid number of arguments for /auth.");
                return null;
            }
        }

        // --- Validation helpers can be private static within AuthMessage too ---
         private static bool IsValidUsername(string u) => u.Length > 0 && u.Length <= 20 && u.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-');
         private static bool IsValidSecret(string s) => s.Length > 0 && s.Length <= 128 && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-');
         private static bool IsValidDisplayName(string d) => d.Length > 0 && d.Length <= 20 && d.All(c => c >= '!' && c <= '~');

        public byte[] GetTcpPayload()
        {
            // Format the string according to the TCP spec
            string dataString = $"AUTH {this.Username} AS {this.DisplayName} USING {this.Secret}\r\n";

            // Encode the string into bytes using ASCII

            byte[] dataBytes = Encoding.ASCII.GetBytes(dataString);

            // Return the byte array
            return dataBytes;
        }


        public override string ToString()
        {
            return string.Format("username: {0} token: {1} displayName: {2}", Username, Secret, DisplayName);
        }
    }
}