using System.Net;
using System.Text;
namespace Ipk25Chat
{
    
    public class ConfirmMessage : Message
    {
        public override MessageType Type => MessageType.CONFIRM;

        
        public MessageType typeOfMessageWasConfirmed {get; private set;}        

        public ConfirmMessage(int messageId) : base (messageId)
        {
           
        }

        
        public ConfirmMessage(int messageId, MessageType type) : base (messageId)
        {
           
            typeOfMessageWasConfirmed = type;
        }

        public override byte[] GetBytesForUdpPacket() // Implementing the requested method
        {

            // Use MemoryStream and BinaryWriter for easy construction
            using (var memoryStream = new MemoryStream())
            // Using BinaryWriter with ASCII encoding
            using (var writer = new BinaryWriter(memoryStream, Encoding.ASCII, false))
            {
                
                writer.Write((byte)this.Type); 

                
                short networkOrderMessageId = IPAddress.HostToNetworkOrder((short)this.MessageId);
                // Write the 2-byte short in network order.
                writer.Write(networkOrderMessageId);

                // Ensure all data is written to the underlying stream
                writer.Flush();
                // Return the contents of the MemoryStream as a byte array
                return memoryStream.ToArray();
            }
        }
    }
}