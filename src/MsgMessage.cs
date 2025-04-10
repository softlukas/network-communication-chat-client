using System.Text;
namespace Ipk25Chat
{
    // Represents a standard MSG chat message data container.
    public class MsgMessage : Message
    {
        // Gets the message type, which is always MSG for this class.
        public override MessageType Type => MessageType.MSG;
        
        public string DisplayName { get; private set; }

        
        public string MessageContent { get; private set; }

        public MsgMessage(string displayName, string messageContent)
        {
            // Initialize the message content
            DisplayName = displayName;
            MessageContent = messageContent;
        }

        public override byte[] GetTcpPayload()
        {
            // Format the message as "MSG FROM {DisplayName} IS {MessageContent}"
            string dataString = $"MSG FROM {this.DisplayName} IS {this.MessageContent}\r\n";
           
            byte[] dataBytes = Encoding.ASCII.GetBytes(dataString);

            // Return the byte array
            return dataBytes;
        }

        public override string ToString()
        {
            // Format the message as "MSG FROM {DisplayName} IS {MessageContent}"
            return $"Server: {DisplayName} {MessageContent}\n";
        }
    }
}