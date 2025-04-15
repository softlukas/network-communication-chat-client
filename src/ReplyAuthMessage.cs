namespace Ipk25Chat {

public class ReplyAuthMessage : Message
{
    public override MessageType Type => MessageType.REPLY;

    public bool IsSuccess { get; private set; }

    public string MessageContent { get; private set; }

    public ReplyAuthMessage(bool isSuccess, string messageContent)
    {
        IsSuccess = isSuccess;
        MessageContent = messageContent;
    }

    public override byte[] GetBytesInTcpGrammar()
    {
        return null;
    }

    public override string ToString()
    {
        if(IsSuccess)
            return $"Action Success: {MessageContent}\n";
        else
            return $"Action Failure: {MessageContent}\n";
    }
}

}