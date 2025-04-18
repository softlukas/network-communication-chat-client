
namespace Ipk25Chat
{
    
    public class PingMessage : Message
    {
        
        public override MessageType Type => MessageType.PING;

        

        public PingMessage(int messageId) : base (messageId)
        {
            

        }
    }
}