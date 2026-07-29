using System.ComponentModel.DataAnnotations;

namespace Sanctuary.Core.Configuration;

public sealed class GatewayServerOptions : ServerOptions
{
    /// <example>live</example>
    [Required]
    public required string Environment { get; set; }

    /// <summary>
    /// Client version the server supports.
    /// </summary>
    /// <example>1.910.1.530630</example>
    [Required]
    public required string ClientVersion { get; set; }

    [Required]
    public required string ServerAddress { get; set; }

    [Required]
    public required string LoginGatewayAddress { get; set; }

    [Required]
    public required string LoginGatewayChallenge { get; set; }

    public bool ShowMemberNagScreen { get; set; }
}