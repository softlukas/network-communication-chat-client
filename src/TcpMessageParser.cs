namespace Ipk25Chat {

public static class TcpMessageParser
{
    public static Message WriteParsedTcpIncomingMessage(string rawData, TcpChatClient tcpChatClient)
    {
        try
        {    
            if (string.IsNullOrWhiteSpace(rawData))
                return null;
            Console.Error.WriteLine($"Debug: rawData: {rawData}");
            // check reply on auth message
            if (rawData.StartsWith("REPLY OK IS ", StringComparison.OrdinalIgnoreCase)) 
            {
                ReplyAuthMessage replyAuthMessage = new ReplyAuthMessage
                (
                    isSuccess: true,
                    messageContent: rawData.Substring(12).TrimEnd('\r','\n')
                );
            
                Console.WriteLine(replyAuthMessage.ToString());

                if(tcpChatClient.CurrentState == ClientState.Auth) {
                    tcpChatClient.CurrentState = ClientState.Open;
                }
                else if(tcpChatClient.CurrentState == ClientState.Join) {
                    tcpChatClient.CurrentState = ClientState.Open;
                }
               
                
                return replyAuthMessage;
            }
            if (rawData.StartsWith("REPLY NOK IS ", StringComparison.OrdinalIgnoreCase)) {
                ReplyAuthMessage replyAuthMessage = new ReplyAuthMessage
                (
                    isSuccess: false,

                    messageContent: rawData.Substring(13).TrimEnd('\r','\n')
                );
                Console.WriteLine(replyAuthMessage.ToString());
                if(tcpChatClient.CurrentState == ClientState.Join)
                {
                    tcpChatClient.CurrentState = ClientState.Open;
                }
                return replyAuthMessage;
            }
        
            if (rawData.StartsWith("MSG FROM ", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = "MSG FROM ";
                string infix = " IS ";
                // Find the position of " IS " after "MSG FROM " (case-insensitive)
                int isIndex = rawData.IndexOf(infix, prefix.Length, StringComparison.OrdinalIgnoreCase);

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

            if(rawData.StartsWith("BYE FROM ", StringComparison.OrdinalIgnoreCase))
            {
                ByeMessage byeMessage = new ByeMessage
                (
                    displayName: rawData.Substring(9).TrimEnd('\r','\n')
                );
                
            
                Console.WriteLine(byeMessage.ToString());

                

                return byeMessage;
            }

            if(rawData.StartsWith("ERR FROM ", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = "ERR FROM ";
                string infix = " IS ";
                // Find the position of " IS " after "ERR FROM "
                int isIndex = rawData.IndexOf(infix, prefix.Length);
                Console.Error.WriteLine($"Debug: isIndex: {isIndex}");
                Console.Error.WriteLine("Here");
                if (isIndex > prefix.Length) // Check if " IS " was found and is after DisplayName
                {
                    // Extract DisplayName (between "ERR FROM " and " IS ")
                    string displayName = rawData.Substring(prefix.Length, isIndex - prefix.Length);

                    Console.Error.WriteLine($"Debug: DisplayName: {displayName}");

                    // Extract MessageContent (after " IS " until CRLF)
                    string messageContent = rawData.Substring(isIndex + infix.Length).TrimEnd('\r','\n');

                    Console.Error.WriteLine($"Debug: MessageContent: {messageContent}");

                    // Create and return ErrMessage object
                    ErrMessage errMessage = new ErrMessage
                    (
                        displayName: displayName,
                        messageContent: messageContent
                    );

                    Console.WriteLine(errMessage.ToString());
                    return errMessage;
                }
                
            }



            Console.WriteLine("ERROR: Unknown message format.");
            return null; // Return null if no known pattern matches
        }
        catch (Exception ex)
        {
            
            Console.Error.WriteLine($"ERROR: Exception during parsing: {ex}");
            Environment.Exit(1);
            return null;
        }
    }
}
}
    