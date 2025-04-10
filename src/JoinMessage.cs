using System.Text; 
namespace Ipk25Chat
{
    // Represents the data required for a JOIN message
    // used by the client to request joining a channel.
    public class JoinMessage : Message // Inherits from the base Message class
    {
        // Gets the message type, which is always JOIN for this class.
        public override MessageType Type => MessageType.JOIN;

        public string ChannelID { get; private set; }

        
        public string DisplayName { get; private set; }

        public JoinMessage(string channelId, string displayName)
        {
            ChannelID = channelId;
            DisplayName = displayName;
        }

        public static string[] ParseJoinMessageArgs(string argsString, TcpChatClient tcpChatClient)
        {
            if (string.IsNullOrWhiteSpace(argsString))
            {
                Console.Error.WriteLine("ERROR (Auth.TryParse): Arguments string is empty.");
                return null;
            }

            // Split arguments, expect 3 parts
            string[] parts = argsString.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                
                return parts;
            }
            else
            {
                // Incorrect number of arguments
                Console.Error.WriteLine("ERROR (Auth.TryParse): Invalid number of arguments for /auth.");
                return null;
            }
        }

        public override byte[] GetTcpPayload()
        {
            
            string dataString = $"JOIN {ChannelID} AS {DisplayName}\r\n";
           
            byte[] dataBytes = Encoding.ASCII.GetBytes(dataString);

            // Return the byte array
            return dataBytes;
        }
    }
}