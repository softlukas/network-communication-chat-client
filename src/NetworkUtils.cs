using System.Net;
using System.Net.Sockets;

namespace Ipk25Chat
{
    // Utility class for network-related operations.
    public static class NetworkUtils
    {
        public static IPAddress ResolveIpAddressFromDns(string serverName)
        {

            // Try to parse the serverName as an IP address
            try {
                return IPAddress.Parse(serverName);
            }
            catch (FormatException)
            {
                // Try to resolve the domain name to an IP address
                try
                {
                    var addresses = Dns.GetHostAddresses(serverName).Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).ToArray();
                    if (addresses.Length > 0)
                    {
                        return addresses[0];
                            
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Invalid server address provided.");
                    Environment.Exit(1);
                }
            }
        
            // If no IP address could be resolved, return null or throw an exception
            return null;
        }
    }
}
    
