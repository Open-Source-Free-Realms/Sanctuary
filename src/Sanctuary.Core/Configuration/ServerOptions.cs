namespace Sanctuary.Core.Configuration;

public class ServerOptions
{
    public const string Section = "Server";

    public required int Port { get; set; }

    public required bool UseCompression { get; set; }

    public required string ProtocolName { get; set; }
}