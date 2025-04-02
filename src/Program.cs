using System;
using CommandLine;


public class Program
{
    public static void Main(string[] args)
    {
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
            // Parsing was successful, we have the 'options' object with values
            
        })
        .WithNotParsed<CliOptions>((errors) =>
        {
            // Parsing failed or the user requested help (-h/--help)
            Console.WriteLine("Argument parsing failed or help requested.");
        });
    }
}