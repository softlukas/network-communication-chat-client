using System.Text;
using System.Net;
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

        public ByeMessage(string displayName, int messageId) : base (messageId)
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

         public override byte[] GetBytesForUdpPacket()
        {
            
            using (var memoryStream = new MemoryStream())
            
            using (var writer = new BinaryWriter(memoryStream, Encoding.ASCII, false))
            {
                
                writer.Write((byte)this.Type);

               
                short networkOrderMessageId = IPAddress.HostToNetworkOrder((short)this.MessageId);

                // messageID without 0
                writer.Write(networkOrderMessageId);

                // displayName + 0
                WriteNullTerminated(writer, this.DisplayName);

                writer.Flush();

                // Return the contents of the MemoryStream as a byte array
                return memoryStream.ToArray();
            }
        }

        
        public override string ToString()
        {
            // Format the message as "BYE {DisplayName}"
            return $"Server: {DisplayName} has left the chat.\n";
        }
    }
}