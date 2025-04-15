using System;
using System.Linq;
using System.Text;
using System.Net;

namespace Ipk25Chat
{
public class AuthMessage : Message
{
    public override MessageType Type => MessageType.AUTH;
    private string _username;

    private string _secret;
    private string _displayName;
    public string Username { get {
        return _username;
    } private set {
        if(!IsValidUsername(value))
        {
            throw new ArgumentException("ERROR: Invalid username.");
        }
        _username = value;
    } }
    public string DisplayName { get {
        return _displayName;
    } private set {
        if(!IsValidDisplayName(value))
        {
            throw new ArgumentException("ERROR: Invalid display name.");
        }
        _displayName = value;
    } }
    public string Secret { get {
        return _secret;
    } private set {
        if(!IsValidSecret(value))
        {
            throw new ArgumentException("ERROR: Invalid secret.");
        }
        _secret = value;
    } }

    public AuthMessage(string username, string secret, string displayName)
    {
        Username = username;
        Secret = secret;
        DisplayName = displayName;
    }

    public AuthMessage(string username, string secret, string displayName, int messageId)
        : base(messageId)
    {
        Username = username;
        Secret = secret;
        DisplayName = displayName;
    }

    public static string[] ParseAuthMessageArgs(string argsString)
    {
        if (string.IsNullOrWhiteSpace(argsString))
        {
            Console.Error.WriteLine("ERROR (Auth.TryParse): Arguments string is empty.");
            return null;
        }

        // Split arguments, expect 3 parts
        string[] parts = argsString.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 3)
        {
            return parts;
        }
        
        // Incorrect number of arguments
        Console.WriteLine("ERROR (Auth.TryParse): Invalid number of arguments for /auth.");
        return null;
        
    }

    
    private static bool IsValidUsername(string u) {
        if(!string.IsNullOrEmpty(u))
        {
            return u.Length <= 21 && u.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-');
        }
        return false;
    }

    private static bool IsValidSecret(string s) {
        if(!string.IsNullOrEmpty(s))
        {
            return s.Length <= 128 && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-');
        }
        return false;
    }

    private static bool IsValidDisplayName(string d) {
        if(!string.IsNullOrEmpty(d))
        {
            return d.Length <= 20 && d.All(c => c >= '!' && c <= '~');
        }
        return false;
    }
    public override byte[] GetBytesInTcpGrammar()
    {
        
        string dataString = $"AUTH {this.Username} AS {this.DisplayName} USING {this.Secret}\r\n";
        Console.WriteLine($"Debug: TCP payload: {dataString}");
       

        byte[] dataBytes = Encoding.ASCII.GetBytes(dataString);

        return dataBytes;
    }

    
   
    public override byte[] GetBytesForUdpPacket() 
    {
        using (var memoryStream = new MemoryStream())
        
        using (var writer = new BinaryWriter(memoryStream, Encoding.ASCII, false))
        {
           
            writer.Write((byte)this.Type); // 0x02

            short networkOrderMessageId = IPAddress.HostToNetworkOrder((short)this.MessageId);
            // Write the 2-byte short MessageId in network order
            writer.Write(networkOrderMessageId);

            
            WriteNullTerminated(writer, this.Username);

           
            WriteNullTerminated(writer, this.DisplayName);

           
            WriteNullTerminated(writer, this.Secret);

            
            writer.Flush();
           
            return memoryStream.ToArray();
        }
    }

    public override string ToString()
    {
        return string.Format("username: {0} token: {1} displayName: {2}", Username, Secret, DisplayName);
    }
}
}