using System;
using System.Threading.Tasks;
using System.Text;

namespace Ipk25Chat
{
    // Message type enum
    public enum MessageType : byte // Explicitly use byte as underlying type
    {
        CONFIRM = 0x00,
        REPLY   = 0x01,
        AUTH    = 0x02,
        JOIN    = 0x03,
        MSG     = 0x04,
        PING    = 0xFD,
        ERR     = 0xFE, 
        BYE     = 0xFF 
    }

    // Base abstract class for message
    public abstract class Message
    {
        public abstract MessageType Type { get; }

        
        // used only if udp and no replymessage is
        public int MessageId {get; private set; } 

        public Message() {
            // Default constructor
        }
        public Message(int messageId) {
            // Constructor with message ID using in UDP
            MessageId = messageId;
        }
        
        public virtual byte[] GetBytesInTcpGrammar() {
            // Default implementation for TCP payload
            Console.Error.WriteLine("GetTcpPayload must call in derived class.");
            return null;
        }

        public virtual byte[] GetBytesForUdpPacket() {
            // Default implementation for UDP packet
            Console.Error.WriteLine("GetBytesForUdpPacket must call in derived class.");
            return null;
        }
        protected void WriteNullTerminated(BinaryWriter writer, string value)
        {
            if (value != null) // Write string bytes if not null
            {
                writer.Write(Encoding.ASCII.GetBytes(value));
            }
            writer.Write((byte)0x00); // Always write the null terminator
        }
  
        protected static bool IsValidUsername(string u) {
            if(!string.IsNullOrEmpty(u))
            {
                return u.Length <= 21 && u.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-');
            }
            return false;
        }

        protected static bool IsValidSecret(string s) {
            if(!string.IsNullOrEmpty(s))
            {
                return s.Length <= 128 && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-');
            }
            return false;
        }

        protected static bool IsValidDisplayName(string d) {
            if(!string.IsNullOrEmpty(d))
            {
                return d.Length <= 20 && d.All(c => c >= '!' && c <= '~');
            }
            return false;
        }

    } // End of Message class
}