using System;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace Ipk25Chat
{
    // This class represents an authentication message in the chat application.
    public class AuthMessage : Message
    {
        // The type of the message, which is AUTH.
        public override MessageType Type => MessageType.AUTH;

        // The username, secret, and display name of the user.
        private string? _username;
        private string? _secret;
        private string? _displayName;

        // Properties for username, secret, and display name with validation.
        public string?Username
        {
            get => _username;
            private set
            {
                if (value == null || !IsValidUsername(value))
                {
                    throw new ArgumentException("ERROR: Invalid username.");
                }
                _username = value;
            }
        }

        public string? DisplayName
        {
            get => _displayName;
            private set
            {
                if (value == null || !IsValidDisplayName(value))
                {
                    throw new ArgumentException("ERROR: Invalid display name.");
                }
                _displayName = value;
            }
        }

        public string? Secret
        {
            get => _secret;
            private set
            {
                if (value == null || !IsValidSecret(value))
                {
                    throw new ArgumentException("ERROR: Invalid secret.");
                }
                _secret = value;
            }
        }

        // Constructor for the AuthMessage class.
        public AuthMessage(string username, string secret, string displayName)
        {
            Username = username;
            Secret = secret;
            DisplayName = displayName;
        }

        // Constructor for the AuthMessage class with message ID.
        public AuthMessage(string username, string secret, string displayName, int messageId)
            : base(messageId)
        {
            Username = username;
            Secret = secret;
            DisplayName = displayName;
        }

        public static string[]? ParseAuthMessageArgs(string argsString)
        {
            // Check if the input string is null or empty
            if (string.IsNullOrWhiteSpace(argsString))
            {
                Console.WriteLine("ERROR (Auth.TryParse): Arguments string is empty.");
                return null;
            }

            // Split arguments, expect 3 parts
            string[] parts = argsString.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 3)
            {
                return parts;
            }

            // Incorrect number of arguments
            Console.WriteLine("ERROR (Auth.TryParse): Invalid number of arguments for /auth.");
            return null;
        }

        public override byte[] GetBytesInTcpGrammar()
        {
            string dataString = $"AUTH {Username} AS {DisplayName} USING {Secret}\r\n";
            return Encoding.ASCII.GetBytes(dataString);
        }

        public override byte[] GetBytesForUdpPacket()
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream, Encoding.ASCII, leaveOpen: false))
            {
                writer.Write((byte)Type); // 0x02

                short networkOrderMessageId = IPAddress.HostToNetworkOrder((short)MessageId);
                // Write the 2-byte short MessageId in network order
                writer.Write(networkOrderMessageId);

                // Write the username, display name, and secret as null-terminated strings
                WriteNullTerminated(writer, Username ?? string.Empty);
                WriteNullTerminated(writer, DisplayName ?? string.Empty);
                WriteNullTerminated(writer, Secret ?? string.Empty);

                writer.Flush();
                return memoryStream.ToArray();
            }
        }

        // Helper method to print the auth message
        public override string ToString()
        {
            return $"username: {Username} token: {Secret} displayName: {DisplayName}";
        }
    }
}