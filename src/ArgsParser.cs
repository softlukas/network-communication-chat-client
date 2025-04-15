using CommandLine;
using System;
using System.Collections.Generic; // Required for WithNotParsed Error handling

// Definition of enum for transport protocol
public enum TransportProtocol
{
    Tcp,
    Udp
}

// Class to hold the argument values
public class CliOptions
{
    [Option('t', "transport", Required = true, HelpText = "Transport protocol to use (tcp or udp).")]
    public TransportProtocol Transport { get; set; }

    [Option('s', "server", Required = true, HelpText = "Server IP address or hostname.")]
    public string Server { get; set; } = string.Empty; // Initialize for C# nullability compliance

    [Option('p', "port", Required = false, Default = (ushort)4567, HelpText = "Server port.")]
    public ushort Port { get; set; } // Using ushort for uint16

    [Option('d', "timeout", Required = false, Default = (ushort)250, HelpText = "UDP confirmation timeout in milliseconds.")]
    public ushort TimeoutMs { get; set; } // Using ushort for uint16

    [Option('r', "retries", Required = false, Default = (byte)3, HelpText = "Maximum number of UDP retransmissions.")]
    public byte MaxRetries { get; set; } // Using byte for uint8
}

