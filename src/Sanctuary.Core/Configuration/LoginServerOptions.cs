using System.ComponentModel.DataAnnotations;

namespace Sanctuary.Core.Configuration;

public sealed class LoginServerOptions : ServerOptions
{
    [Required]
    public required int LoginGatewayPort { get; set; }

    [Required]
    public required string LoginGatewayChallenge { get; set; }

    /// <summary>
    /// Locks the server and only allows admins to login.
    /// </summary>
    public bool IsLocked { get; set; }

    public int DefaultTitleId { get; set; }

    [Required]
    public required int DefaultProfileId { get; set; }

    public int StartingCoins { get; set; }
    public int StartingStationCash { get; set; }

    public bool UnlockAllTitles { get; set; }
    public bool UnlockAllProfiles { get; set; }
}