
using System.Collections.Concurrent; 
using System.Threading;             
using System.Net;                   


namespace Ipk25Chat
{
    
    public class UdpSentMessageInfo
    {
        public int MessageId { get; init; }
        public byte[] Payload { get; init; }
        public IPEndPoint TargetEndPoint { get; init; }
        public int RetryCount { get; set; } = 0;
        public Timer? RetransmissionTimer { get; set; }
        public DateTime LastSentTime { get; set; }

        public UdpSentMessageInfo(int messageId, byte[] payload, IPEndPoint targetEndPoint)
        {
            this.MessageId = messageId;
            this.Payload = (byte[])payload.Clone();
            this.TargetEndPoint = targetEndPoint;
            this.LastSentTime = DateTime.UtcNow;
        }
    }
}