using System;
using CommandLine;


namespace Ipk25Chat {
    public class Program
    {
        public static void Main(string[] args)
        {
            TcpChatClient tcpClient = null;
            // Create a parser instance with default settings
            var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true; // Allow tcp/TCP, udp/UDP for -t (case-insensitive)
                with.HelpWriter = Console.Error; // By default, writes help output to Console.Error
            });

            // Parse command line arguments
            var parserResult = parser.ParseArguments<CliOptions>(args);

            // Handle the parsing result
            parserResult.WithParsed<CliOptions>(options =>
            {
                // Parsing succeeded
                tcpClient = new TcpChatClient(options.Server, options.Port);
                
            })
            .WithNotParsed<CliOptions>((errors) =>
            {
                // Parsing failed or the user requested help (-h/--help)
                Console.WriteLine("Argument parsing failed or help requested.");
            });

            Console.WriteLine(tcpClient.ToString());
        }
    }
}
