
using System;

namespace Ipk25Chat
{
    
    public class ErrMessage : Message // Inherits from the base Message class
    {
        
        public override MessageType Type => MessageType.ERR;

        
        public string DisplayName { get; private set; }

        
        public string MessageContent { get; private set; }

        
        
        public ErrMessage(string displayName, string messageContent)
        {
           
            this.DisplayName = displayName;
            this.MessageContent = messageContent;
        }

        public override string ToString()
        {
            // Format the message as "ERR {DisplayName} IS {MessageContent}"
            return $"ERROR FROM {this.DisplayName}: {this.MessageContent}\n";
        }
    }
}