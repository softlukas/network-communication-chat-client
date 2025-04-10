namespace Ipk25Chat {

public class ReplyAuthMessage : Message
{
    // Gets the message type, which is always REPLY for this class.
    public override MessageType Type => MessageType.REPLY;

    public bool IsSuccess { get; private set; }

    public string MessageContent { get; private set; }

    public ReplyAuthMessage(bool isSuccess, string messageContent)
    {
        IsSuccess = isSuccess;
        MessageContent = messageContent;
    }

    public override byte[] GetTcpPayload()
    {
        return null;
    }

    public override string ToString()
    {
        // Format the message as "REPLY <Result> <MessageContent>"
        if(IsSuccess)
            return $"Action Success: {MessageContent}\n";
        else
            return $"Action Failure: {MessageContent}\n";
    }
}

}