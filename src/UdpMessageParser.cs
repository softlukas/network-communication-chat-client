using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Ipk25Chat
{
    public static class UdpMessageParser
    {
        // This method creates a message object based on user input.
        public static Message? CreateMessageFromUserInputAsync(int nextMessageId, UdpChatClient udpChatClient)
        {
            // read user input
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

            if (trimmedInput.StartsWith("/rename ", StringComparison.OrdinalIgnoreCase))
            {
                string newDisplayName = trimmedInput.Substring("/rename ".Length);

                if (string.IsNullOrWhiteSpace(newDisplayName))
                {
                    Console.WriteLine("ERROR: Display name cannot be empty.");
                    return null;
                }
                // change display name
                udpChatClient.DisplayName = newDisplayName;
                // terminate processing any other command
                throw new ArgumentException("rename");
            }
            // help command hendler
            if (trimmedInput.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
            {
                
                throw new ArgumentException("help");
            }
            
            // Check for /auth command
            if (trimmedInput.StartsWith("/auth ", StringComparison.OrdinalIgnoreCase))
            {
                // Extract arguments string
                string argsString = trimmedInput.Substring("/auth ".Length);

                // Call static parser on AuthMessage class
                
                string[]? parsedArgs = AuthMessage.ParseAuthMessageArgs(argsString);
                if (parsedArgs == null || parsedArgs.Length < 3)
                {
                    throw new ArgumentException("Invalid arguments for /auth command.");
                }

                return new AuthMessage
                (
                    username: parsedArgs[0],
                    secret: parsedArgs[1],
                    displayName: parsedArgs[2],
                    messageId: nextMessageId
                );
            }

            // Check for /join command in open state
            if (trimmedInput.StartsWith("/join ", StringComparison.OrdinalIgnoreCase) && udpChatClient.CurrentState == ClientState.Open)
            {
                
                string argsString = trimmedInput.Substring("/join ".Length);

                string[] parsedArgs = JoinMessage.ParseJoinMessageArgs(argsString);
                return new JoinMessage
                (
                    channelId: parsedArgs[0],
                    displayName: udpChatClient.DisplayName,
                    messageId: nextMessageId
                );
                
                
            }
            // any imput is possible in open state
            if (udpChatClient.CurrentState == ClientState.Open && trimmedInput != "/quit") 
            {
                
                
                return new MsgMessage
                (
                    displayName: udpChatClient.DisplayName, 
                    messageContent: trimmedInput,
                    messageId: nextMessageId
                );
                
            }
            
            // bye message created when user types /quit            
            
            if(trimmedInput == "/quit")
            {
                return new ByeMessage(
                    displayName: udpChatClient.DisplayName
                );
            }
            
            // represents invalid command

            return null;
            
        }



        // This method parses incoming UDP messages and returns the appropriate message object.
        public static Message? ParseIncommingUdpMessage(byte[] data)
        {
            if (data == null || data.Length < 3) return null; // Need at least Type + ID

            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms, Encoding.ASCII, false))
            {
                try
                {

                    
                    // Read the first byte to determine the message type
                    byte messageTypeByte = reader.ReadByte();
                    // Convert byte to enum
                    MessageType messageType = (MessageType)messageTypeByte; // Cast byte to enum
                    // Read the next 2 bytes for the message ID
                    short networkMessageId = reader.ReadInt16(); // Reads 2 bytes
                    ushort messageId = (ushort)IPAddress.NetworkToHostOrder(networkMessageId);
                    Console.Error.WriteLine("Debug: Received message type: " + messageType);
                    // switch based on the message type
                    switch (messageType)
                    {
                        case MessageType.REPLY:
                            byte result = reader.ReadByte();
                            short networkRefId = reader.ReadInt16();
                            ushort refMsgId = (ushort)IPAddress.NetworkToHostOrder(networkRefId); // Read RefId for REPLY
                            string content = ReadNullTerminatedString(reader);
                            // create ReplyAuthMessage with ref message ID to check of wtich messsage reply is
                            ReplyAuthMessage replyAuthMsg = new ReplyAuthMessage(
                                isSuccess: result == 1,
                                messageContent: content,
                                messageId: refMsgId
                            );
                            
                            return replyAuthMsg;

                        // common logic for other messages types
                        case MessageType.MSG:
                            string displayName = ReadNullTerminatedString(reader);
                            string msgContent = ReadNullTerminatedString(reader);
                            MsgMessage msgMessage = new MsgMessage(displayName, msgContent, messageId);
                            return msgMessage;

                        case MessageType.ERR:
                            string errDisplayName = ReadNullTerminatedString(reader);
                            string errContent = ReadNullTerminatedString(reader);
                            ErrMessage errMessage = new ErrMessage(errDisplayName, errContent, messageId);
                            return errMessage;

                        case MessageType.BYE:
                            string byeDisplayName = ReadNullTerminatedString(reader);
                            ByeMessage byeMessage = new ByeMessage(byeDisplayName, messageId);
                            return byeMessage;

                        case MessageType.CONFIRM:
                            ConfirmMessage? confirmMessage = null;

                            if (UdpChatClient._pendingConfirmationMessages.ContainsKey(messageId))
                            {
                                // get the info about message wtich is now confirmed
                                UdpSentMessageInfo udpSentMessageInfo = UdpChatClient._pendingConfirmationMessages[messageId];
                                // check if the message is a bye message
                                if (udpSentMessageInfo.Payload[0] == 0xFF)
                                {
                                    // add extra info that is confirmed bye message for later use this info to close client
                                    confirmMessage = new ConfirmMessage(messageId, MessageType.BYE);
                                }
                                // if confirmed mesage is not reply message
                                if (udpSentMessageInfo.Payload[0] != 0x01)
                                {
                                    // mark message as confirmed and delete it from pending messages to confirm
                                    Console.Error.WriteLine("Debug: message with ID " + messageId + " was confirmed.");
                                    UdpChatClient._pendingConfirmationMessages.Remove(messageId);
                                    foreach (var key in UdpChatClient._pendingConfirmationMessages.Keys)
                                    {
                                        Console.Error.WriteLine("Pending confirmation message ID: " + key);
                                    }
                                    if(messageType != MessageType.CONFIRM) {
                                        Console.Error.WriteLine($"message with ID {messageId} and type {messageType} added to already proccessed list");
                                        UdpChatClient.alreadyConfirmedIds.Add(messageId);
                                    }
                                    
                                }
                                confirmMessage = new ConfirmMessage(messageId);
                            }
                            return confirmMessage;

                        case MessageType.PING:
                            PingMessage pingMessage = new PingMessage(messageId);
                            return pingMessage;

                        default:
                            throw new ArgumentException("malformed message");
                            
                            
                    }
                }
                catch (Exception ex)
                {
                    
                    throw new Exception(ex.Message);
                }
            }
        }

        // Helper method to read null-terminated string
        private static string ReadNullTerminatedString(BinaryReader reader)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0x00)
            {
                bytes.Add(b);
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }
    }
}
