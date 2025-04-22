namespace Ipk25Chat {

public static class TcpMessageParser
{

    public static async Task<Message?> CreateMessageFromUserInputAsync(TcpChatClient tcpChatClient)
    {
        // Read line async (non-blocking)
        string? userInput = await Task.Run(() => Console.ReadLine());

        if(userInput == null)
        {
            throw new ArgumentNullException("User input is null");
        }

        

        // Handle empty or EOF input
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return null;
        }

        // Trim whitespace
        string trimmedInput = userInput.Trim();

        //Console.Error.WriteLine($"Debug: User input: {trimmedInput}");

        // Check for /auth command
        if (trimmedInput.StartsWith("/auth ", StringComparison.OrdinalIgnoreCase))
        {
            // Extract arguments string
            string argsString = trimmedInput.Substring("/auth ".Length);

            // Call static parser on AuthMessage class
            string[] parsedArgs = AuthMessage.ParseAuthMessageArgs(argsString);
            tcpChatClient.DisplayName = parsedArgs[2];
            AuthMessage? authMessage = null;
            try {
                authMessage =  new AuthMessage
                (
                username: parsedArgs[0],
                secret: parsedArgs[1],
                displayName: parsedArgs[2]
                );

                if(tcpChatClient.CurrentState != ClientState.Join && tcpChatClient.CurrentState != ClientState.Open)
                {
                    tcpChatClient.CurrentState = ClientState.Auth;
                }
                
                
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ex.Message);
            }
            
            return authMessage;
        }

        if (trimmedInput.StartsWith("/rename ", StringComparison.OrdinalIgnoreCase))
        {
            string newDisplayName = trimmedInput.Substring("/rename ".Length);

            if (string.IsNullOrWhiteSpace(newDisplayName))
            {
                Console.Error.WriteLine("Error: Display name cannot be empty.");
                return null;
            }

            tcpChatClient.DisplayName = newDisplayName;
            throw new ArgumentException("rename");
        }
        
        if (tcpChatClient.CurrentState == ClientState.Open && trimmedInput != "/quit" && !trimmedInput.Contains("/join"))
        {
            
            Console.Error.WriteLine("Debug: Msg message object created");
            
            return new MsgMessage
            (
                displayName: tcpChatClient.DisplayName, 
                messageContent: trimmedInput
            );
            
        }
        if (trimmedInput.StartsWith("/join ", StringComparison.OrdinalIgnoreCase))
        {
            
            string argsString = trimmedInput.Substring("/join ".Length);

            // Parse arguments specifically for Join
            string[] parsedArgs = JoinMessage.ParseJoinMessageArgs(argsString);
            
            return new JoinMessage
            (
                channelId: parsedArgs[0],
                displayName: tcpChatClient.DisplayName
            );
            
        }

        

        if(trimmedInput == "/quit")
        {
            tcpChatClient.CurrentState = ClientState.End;
            return new ByeMessage(
                displayName: tcpChatClient.DisplayName
            );
        }

        if(trimmedInput == "/help")
        {
            PrintHelp();
        }
        
        return null;
    }

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
                int isIndex = rawData.IndexOf(infix, prefix.Length, StringComparison.OrdinalIgnoreCase);
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
    