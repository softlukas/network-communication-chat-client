using System.Text;
using System.Net;
namespace Ipk25Chat
{
    public class MsgMessage : Message
    {
        // Gets the message type, which is always MSG for this class
        public override MessageType Type => MessageType.MSG;
        
        public string DisplayName { get; private set; }

        private string _messageContent;
        public string MessageContent {
        get {

            return _messageContent;

        } private set {
            if(!IsValidContent(value))
            {
                throw new ArgumentException("ERROR: Invalid message content.");
            }
            _messageContent = value;
        }
        }


        public MsgMessage(string displayName, string messageContent)
        {
            // Initialize the message content
            DisplayName = displayName;
            MessageContent = messageContent;
        }

        public MsgMessage(string displayName, string messageContent, int messageId) : base(messageId)
        {
            // Initialize the message content with messageId (udp)
            DisplayName = displayName;
            MessageContent = messageContent;
        }

        
        
        private static bool IsValidContent(string? content)
        {
            if (string.IsNullOrEmpty(content) || content.Length > 60000)
            {
                return false;
            }

            return content.All(c => (c >= '!' && c <= '~') || c == ' ' || c == '\n');      
        }
        

        public override byte[] GetBytesInTcpGrammar()
        {
            // Format the message as "MSG FROM {DisplayName} IS {MessageContent}"
            string dataString = $"MSG FROM {this.DisplayName} IS {this.MessageContent}\r\n";
           
            byte[] dataBytes = Encoding.ASCII.GetBytes(dataString);

           
            return dataBytes;
        }

        public override byte[] GetBytesForUdpPacket()
        {
            
            using (var memoryStream = new MemoryStream())
            
            using (var writer = new BinaryWriter(memoryStream, Encoding.ASCII, false))
            {
                
                writer.Write((byte)this.Type); // 0x04

               
                short networkOrderMessageId = IPAddress.HostToNetworkOrder((short)this.MessageId);

                // messageID without 0
                writer.Write(networkOrderMessageId);

                // displayName + 0
                WriteNullTerminated(writer, this.DisplayName);

                // Message content + 0
                WriteNullTerminated(writer, this.MessageContent);

               
                writer.Flush();

                // Return the contents of the MemoryStream as a byte array
                return memoryStream.ToArray();
            }
        }

        public override string ToString()
        {
            return $"{DisplayName}: {MessageContent}";
        }
    }
}