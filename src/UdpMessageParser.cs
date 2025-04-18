using System.Text;
using System.Net;

namespace Ipk25Chat {
public class UdpMessageParser
{
    
    public static Message ParseUdp(byte[] data)
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

                Message? parsedMsg = null;
                switch (messageType)
                {
                     // Only parse messages expected from server typically
                     case MessageType.REPLY:
                        
                        byte result = reader.ReadByte();
                        short networkRefId = reader.ReadInt16();

                        ushort refMsgId = (ushort)IPAddress.NetworkToHostOrder(networkRefId); // Read RefId for REPLY
                        string content = ReadNullTerminatedString(reader);
                        ReplyAuthMessage replyAuthMsg = new ReplyAuthMessage
                        (
                            isSuccess: result == 1,
                            messageContent: content
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
                          // parsedMsg = new ErrMessage { DisplayName = errDisplayName, MessageContent = errContent };
                          break;
                      case MessageType.BYE:
                           string byeDisplayName = ReadNullTerminatedString(reader);
                            ByeMessage byeMessage = new ByeMessage(byeDisplayName, messageId);
                            return byeMessage;
                           // parsedMsg = new ByeMessage { DisplayName = byeDisplayName };
                           break;
                      case MessageType.CONFIRM: // Client receives CONFIRM from server

                            Console.Error.WriteLine("Debug: byty co dosli ako reply");

                            foreach(var b in data)
                            {
                                Console.Error.Write(b + " ");
                            }

                            Console.Error.WriteLine();

                            ConfirmMessage confirmMessage = null;


                            Console.Error.WriteLine("Debug: message with ID " + messageId + " was confirmed.");
                            if (UdpChatClient._pendingConfirmationMessages.ContainsKey(messageId))
                            {
                                UdpSentMessageInfo udpSentMessageInfo = UdpChatClient._pendingConfirmationMessages[messageId];
                                // bye message was confirmed
                                if(udpSentMessageInfo.Payload[0] == 0xFF) {
                                    confirmMessage = new ConfirmMessage(messageId, MessageType.BYE);
                                }
                                UdpChatClient._pendingConfirmationMessages.Remove(messageId);
                                UdpChatClient.alreadyConfirmedIds.Add(messageId);
                            }
                           //parsedMsg = new ConfirmMessage { RefMessageId = confirmRefId };
                           return confirmMessage;
                           break;
                      case MessageType.PING: // Client receives PING from server
                            PingMessage pingMessage = new PingMessage(messageId);
                            return pingMessage;
                           
                     default:
                           Console.Error.WriteLine($"WARN: UDP Parser received unknown message type byte: {messageTypeByte}");
                           break; // Parsed message remains null
                }
                return null;
            }
            catch (Exception ex) { Console.Error.WriteLine($"ERROR: Exception during UDP parsing: {ex}"); return null; }
        }
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