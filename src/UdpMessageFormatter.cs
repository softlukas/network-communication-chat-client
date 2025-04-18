using System.Text;
using System.Net;
namespace Ipk25Chat
{
   
public class UdpMessageFormatter
{


    public static Message? CreateMessageFromUserInputAsync(int nextMessageId, UdpChatClient udpChatClient)
    {
        
        string? userInput = Console.ReadLine();

        // ctrl D
        if(userInput == null)
        {
            throw new ArgumentNullException("ERROR: User input is null.");
        }

        // Handle empty input
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
            return new AuthMessage
            (
                username: parsedArgs[0],
                secret: parsedArgs[1],
                displayName: parsedArgs[2],
                messageId: nextMessageId
            );
        }
        if (udpChatClient.CurrentState == ClientState.Open && trimmedInput != "/quit") 
        {
            Console.Error.WriteLine("Debug: Msg message object created");
            
            return new MsgMessage
            (
                displayName: udpChatClient.DisplayName, 
                messageContent: trimmedInput,
                messageId: nextMessageId
            );
            
        }
        
        if (udpChatClient.CurrentState == ClientState.Open && trimmedInput != "/quit")
        {
            Console.Error.WriteLine("Debug: Msg message object created");
            
            return new MsgMessage
            (
                displayName: udpChatClient.DisplayName, 
                messageContent: trimmedInput
            );
            
        }
        if (trimmedInput.StartsWith("/join ", StringComparison.OrdinalIgnoreCase))
        {
            /*
            string argsString = trimmedInput.Substring("/join ".Length);

            // Parse arguments specifically for Join
            //TODO
            //string[] parsedArgs = JoinMessage.ParseJoinMessageArgs(argsString, udpChatClient);
            return new JoinMessage
            (
                channelId: parsedArgs[0],
                displayName: udpChatClient.DisplayName
            );
            */
            
        }
        if(trimmedInput == "/quit")
        {
            udpChatClient.CurrentState = ClientState.End;
            return new ByeMessage(
                displayName: udpChatClient.DisplayName
            );
        }
        
        

        return null;
        
    }
}
}