using System.Net;
using System.Text;
using System;

namespace Ipk25Chat
{
    
    public class ErrMessage : Message // Inherits from the base Message class
    {
        
        public override MessageType Type => MessageType.ERR;

        
        public string DisplayName { get; private set; }

        
        public string MessageContent { get; private set; }

        
        
        public ErrMessage(string displayName, string messageContent)
        {
           
            this.DisplayName = displayName;
            this.MessageContent = messageContent;
        }

        public ErrMessage(string displayName, string messageContent, int messageId) : base(messageId)
        {
            this.DisplayName = displayName;
            this.MessageContent = messageContent;
        }

        public override string ToString()
        {
            // Format the message as "ERR {DisplayName} IS {MessageContent}"
            return $"ERROR FROM {this.DisplayName}: {this.MessageContent}\n";
        }

        public override byte[] GetBytesInTcpGrammar()
        {
            // Format the message as "ERR {DisplayName} IS {MessageContent}\r\n"
            string dataString = $"ERR FROM {this.DisplayName} IS {this.MessageContent}\r\n";
            
            // Convert the string to a byte array using ASCII encoding
            byte[] dataBytes = Encoding.ASCII.GetBytes(dataString);
            
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

                // Message content + 0
                WriteNullTerminated(writer, this.MessageContent);

               
                writer.Flush();

                // Return the contents of the MemoryStream as a byte array
                return memoryStream.ToArray();
            }
        }
    }
}