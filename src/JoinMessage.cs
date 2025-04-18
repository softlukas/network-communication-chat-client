using System.Net;
using System.Text; 
namespace Ipk25Chat
{
    // Represents the data required for a JOIN message
    public class JoinMessage : Message
    {
        // Gets the message type, which is always JOIN for this class
        public override MessageType Type => MessageType.JOIN;

        public string ChannelID { get; private set; }

        
        public string DisplayName { get; private set; }

        public JoinMessage(string channelId, string displayName)
        {
            ChannelID = channelId;
            DisplayName = displayName;
        }


        public JoinMessage(string channelId, string displayName, int messageId) : base(messageId)
        {
            ChannelID = channelId;
            DisplayName = displayName;
        }


        public static string[] ParseJoinMessageArgs(string argsString)
        {
            if (string.IsNullOrWhiteSpace(argsString))
            {
                Console.Error.WriteLine("ERROR (Auth.TryParse): Arguments string is empty.");
                return null;
            }

            // Split arguments, expect 1 part
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

        public override byte[] GetBytesInTcpGrammar()
        {
            
            string dataString = $"JOIN {ChannelID} AS {DisplayName}\r\n";
           
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


                WriteNullTerminated(writer, this.ChannelID);

                // displayName + 0
                WriteNullTerminated(writer, this.DisplayName);

            
                writer.Flush();

                // Return the contents of the MemoryStream as a byte array
                return memoryStream.ToArray();
            }
        }

       
    }
}