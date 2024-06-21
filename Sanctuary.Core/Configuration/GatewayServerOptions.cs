namespace Sanctuary.Core.Configuration;

public sealed class GatewayServerOptions : ServerOptions
{
    /// <example>live</example>
    public required string Environment { get; set; }

    /// <summary>
    /// Client version the server supports.
    /// </summary>
    /// <example>1.910.1.530630</example>
    public required string ClientVersion { get; set; }

    public required string ServerAddress { get; set; }

    public required string LoginGatewayAddress { get; set; }
    public required string LoginGatewayChallenge { get; set; }

    public bool ShowMemberNagScreen {  get; set; }
}