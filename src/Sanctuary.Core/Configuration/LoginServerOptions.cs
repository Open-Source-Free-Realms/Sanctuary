namespace Sanctuary.Core.Configuration;

public sealed class LoginServerOptions : ServerOptions
{
    /// <summary>
    /// Key used in encrypting/decrypting packets for the Login Server.
    /// </summary>
    /// <value>
    /// Encoded in Base64, Hardcoded in the Client after initializing <c>Login::ExternalLoginUdpApi</c>.
    /// </value>
    public required string CryptKey { get; set; }

    public required int LoginGatewayPort { get; set; }
    public required string LoginGatewayChallenge { get; set; }

    /// <summary>
    /// Locks the server and only allows admins to login.
    /// </summary>
    public bool IsLocked { get; set; }

    public int DefaultTitleId { get; set; }
    public required int DefaultProfileId { get; set; }

    public required float DefaultPositionX { get; set; }
    public required float DefaultPositionY { get; set; }
    public required float DefaultPositionZ { get; set; }

    public required float DefaultRotationX { get; set; }
    public required float DefaultRotationZ { get; set; }

    public bool UnlockAllItems { get; set; }
    public bool UnlockAllTitles { get; set; }
    public bool UnlockAllMounts { get; set; }
    public bool UnlockAllProfiles { get; set; }
}