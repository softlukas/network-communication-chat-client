using System.Text;
using System.Net;

namespace Ipk25Chat {
public class UdpMessageParser
{
    
    public static Message ParseUdp(byte[] data, UdpChatClient udpChatClient)
    {
        if (data == null || data.Length < 3) return null; // Need at least Type + ID

        using (var ms = new MemoryStream(data))
        using (var reader = new BinaryReader(ms, Encoding.ASCII, false))
        {
            try
            {
                byte messageTypeByte = reader.ReadByte();
                
                MessageType messageType = (MessageType)messageTypeByte; // Cast byte to enum

                short networkMessageId = reader.ReadInt16(); // Reads 2 bytes
                ushort messageId = (ushort)IPAddress.NetworkToHostOrder(networkMessageId);
                //Console.Error.WriteLine("doslo mi message s id: " + messageId);

                // send back confirm message imidiatly
                //ConfirmMessage confrimation = new ConfirmMessage(messageId);

                //udpChatClient.SendUdpPayloadToServer(confrimation.GetBytesForUdpPacket());

                Message? parsedMsg = null;
                switch (messageType)
                {
                     // Only parse messages expected from server typically
                     case MessageType.REPLY:
                        //Console.Error.WriteLine("Here");
                        byte result = reader.ReadByte();
                        short networkRefId = reader.ReadInt16();

                        ushort refMsgId = (ushort)IPAddress.NetworkToHostOrder(networkRefId); // Read RefId for REPLY
                        string content = ReadNullTerminatedString(reader);
                        ReplyAuthMessage replyAuthMsg = new ReplyAuthMessage
                        (
                            isSuccess: result == 1,
                            messageContent: content,
                            messageId: refMsgId
                            //messageId: messageId
                        );
                        Console.WriteLine(replyAuthMsg.ToString());
                        return replyAuthMsg;
                        //
                        //parsedMsg = new ReplyAuthMessage { IsSuccess = (result == 1), MessageContent = content , RefMessageId = refMsgId  }; // Assuming ReplyMessage has RefMsgId for UDP
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
                           // parsedMsg = new ByeMessage { DisplayName = byeDisplayName };
                           break;
                      case MessageType.CONFIRM: // Client receives CONFIRM from server

                            //Console.Error.WriteLine("Debug: byty co dosli ako reply");

                            

                            //Console.Error.WriteLine();

                            ConfirmMessage confirmMessage = null;


                            
                            if (UdpChatClient._pendingConfirmationMessages.ContainsKey(messageId))
                            {
                                UdpSentMessageInfo udpSentMessageInfo = UdpChatClient._pendingConfirmationMessages[messageId];

                                
                                
                                // bye message was confirmed
                                if(udpSentMessageInfo.Payload[0] == 0xFF) {
                                    confirmMessage = new ConfirmMessage(messageId, MessageType.BYE);
                                }
                                // if reaply message was confirm - do not remove if from pending list
                                if(udpSentMessageInfo.Payload[0] != 0x01) {
                                    Console.Error.WriteLine("Debug: message with ID " + messageId + " was confirmed.");
                                    UdpChatClient._pendingConfirmationMessages.Remove(messageId);
                                    foreach (var key in UdpChatClient._pendingConfirmationMessages.Keys)
                                    {
                                        Console.Error.WriteLine("Pending confirmation message ID: " + key);
                                    }

                                    //UdpChatClient.alreadyConfirmedIds.Add(messageId);
                                }
                                
                            }
                           //parsedMsg = new ConfirmMessage { RefMessageId = confirmRefId };
                           return confirmMessage;
                           break;
                      case MessageType.PING: // Client receives PING from server
                            PingMessage pingMessage = new PingMessage(messageId);
                            return pingMessage;
                           
                     default:
                        //throw new NotSupportedException($"Malfomred message with id: {messageId}");
                        Console.WriteLine($"ERROR: Malformed message with id: {messageId}");
                        return null;
                         // Parsed message remains null
                }
                return null;
            }
            catch (Exception ex) { //Console.Error.WriteLine($"ERROR: Exception during UDP parsing: {ex}"); return null; }
            }
        }
        return null;
    }

    // Helper method to read null-terminated string
    private static string ReadNullTerminatedString(BinaryReader reader) {
         var bytes = new List<byte>();
         byte b;
         while ((b = reader.ReadByte()) != 0x00) { bytes.Add(b); }
         return Encoding.ASCII.GetString(bytes.ToArray());
    }
    
}
}