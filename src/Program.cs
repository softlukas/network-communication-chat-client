using System;
using System.Threading.Tasks;
using CommandLine;


namespace Ipk25Chat {
    public class Program
    {
        public static async Task Main(string[] args)
        {
            
            TcpChatClient tcpClient = null;
            UdpChatClient udpClient = null;
            // Create a parser instance with default settings
            var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true; // Allow tcp/TCP, udp/UDP for -t (case-insensitive)
                with.HelpWriter = Console.Error; // By default, writes help output to Console.Error
            });

            // Parse command line arguments
            var parserResult = parser.ParseArguments<CliOptions>(args);

            // success parsing
            parserResult.WithParsed<CliOptions>(options =>
            {
                
                if(options.Transport == TransportProtocol.Tcp) {
                    tcpClient = new TcpChatClient(options.Server, options.Port);
                }

                else if (options.Transport == TransportProtocol.Udp) {
                    Console.Error.WriteLine($"Debug: Starting UDP Client (Timeout: {options.TimeoutMs}ms, Retries: {options.MaxRetries})");
                   
                    udpClient = new UdpChatClient(options.Server, options.Port, options.TimeoutMs, options.MaxRetries); 
                    
                }
                
                
            })
            .WithNotParsed<CliOptions>((errors) =>
            {
                // Parsing failed or the user requested help (-h/--help)
                Console.Error.WriteLine("Argument parsing failed or help requested.");
            });

           
            if(tcpClient != null) {
                Console.CancelKeyPress += tcpClient.Console_CancelKeyPress;
                await tcpClient.Start();
            }
            else if(udpClient != null) {
                Console.CancelKeyPress += udpClient.Console_CancelKeyPress;
                await udpClient.RunAsync();
            }
            else {
                Console.Error.WriteLine("ERROR: No client created.");
            }
            
        }

        
    }
}
