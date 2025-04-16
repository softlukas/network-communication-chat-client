using System.Text;
namespace Ipk25Chat
{
    public class ByeMessage : Message // Dedí od základnej triedy Message
    {
        // Gets the message type, which is always BYE for this class.
        public override MessageType Type => MessageType.BYE;

        // The display name of the party initiating the disconnection.
        // Constraint: Max 20 chars, Printable characters (0x21-7E).
        public string DisplayName { get; private set; }

        public ByeMessage(string displayName)
        {
            DisplayName = displayName;
        }
        public override byte[] GetBytesInTcpGrammar()
        {
            // Format the message as "BYE {DisplayName}\r\n"
            string dataString = $"BYE FROM {this.DisplayName}\r\n";

            // Convert the string to a byte array using ASCII encoding
            byte[] dataBytes = Encoding.ASCII.GetBytes(dataString);

            // Return the byte array
            return dataBytes;
        }
        public override string ToString()
        {
            // Format the message as "BYE {DisplayName}"
            return $"Server: {DisplayName} has left the chat.\n";
        }
    }
}