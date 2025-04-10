namespace Ipk25Chat {

public static class TcpMessageParser
{
    public static Message WriteParsedTcpIncomingMessage(string rawData, TcpChatClient tcpChatClient)
    {
        try
        {    
            if (string.IsNullOrWhiteSpace(rawData))
                return null;
            // check reply on auth message
            if (rawData.StartsWith("REPLY OK IS ")) 
            {
                ReplyAuthMessage replyAuthMessage = new ReplyAuthMessage
                (
                    isSuccess: true,
                    messageContent: rawData.Substring(12).TrimEnd('\r','\n')
                );
            
                Console.WriteLine(replyAuthMessage.ToString());

                tcpChatClient.CurrentState = ClientState.Open;
                
                Console.Error.WriteLine("Debug: Change state to " + tcpChatClient.CurrentState.ToString());

                return replyAuthMessage;
            }
            if (rawData.StartsWith("REPLY NOK IS ")) {
                ReplyAuthMessage replyAuthMessage = new ReplyAuthMessage
                (
                    isSuccess: false,

                    messageContent: rawData.Substring(13).TrimEnd('\r','\n')
                );
                Console.WriteLine(replyAuthMessage.ToString());
                return replyAuthMessage;
            }
        
            if (rawData.StartsWith("MSG FROM "))
            {
                string prefix = "MSG FROM ";
                string infix = " IS ";
                // Find the position of " IS " after "MSG FROM "
                int isIndex = rawData.IndexOf(infix, prefix.Length);

                if (isIndex > prefix.Length) // Check if " IS " was found and is after DisplayName
                {
                    // Extract DisplayName (between "MSG FROM " and " IS ")
                    string displayName = rawData.Substring(prefix.Length, isIndex - prefix.Length);
                    // Extract MessageContent (after " IS " until CRLF)
                    string messageContent = rawData.Substring(isIndex + infix.Length).TrimEnd('\r','\n');

                    // Create and return MsgMessage object
                    MsgMessage msgMessage = new MsgMessage
                    (
                        displayName: displayName,
                        messageContent: messageContent
                    );
                    Console.WriteLine(msgMessage.ToString());
                    return msgMessage;
                }
                else
                {
                    Console.Error.WriteLine($"WARN: Malformed MSG message (missing ' IS '): {rawData.TrimEnd()}");
                    Environment.Exit(1);
                    return null;
                }
            }

            if(rawData.StartsWith("BYE FROM "))
            {
                ByeMessage byeMessage = new ByeMessage
                (
                    displayName: rawData.Substring(9).TrimEnd('\r','\n')
                );
                
            
                Console.WriteLine(byeMessage.ToString());

                

                return byeMessage;
            }

            Console.Error.WriteLine($"WARN: TcpMessageParser could not parse: {rawData.TrimEnd()}");
            Environment.Exit(1);
            return null; // Return null if no known pattern matches
        }
        catch (Exception ex)
        {
            // Log the exception and return null
            Console.Error.WriteLine($"ERROR: Exception during parsing: {ex}");
            Environment.Exit(1);
            return null; // Return null if parsing fails
        }
    }

}
}
