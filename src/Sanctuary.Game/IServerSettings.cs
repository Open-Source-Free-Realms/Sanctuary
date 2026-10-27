using Sanctuary.Packet;

namespace Sanctuary.Game;

/// <summary>
/// Runtime server state that referees can change in-game without a restart.
/// Nothing here is persisted; a restart restores the defaults.
/// </summary>
public interface IServerSettings
{
    /// <summary>
    /// When set, only referees (mods/admins) can enter the world.
    /// </summary>
    bool IsLocked { get; set; }

    string? MotdTitle { get; set; }
    string? MotdMessage { get; set; }

    float GameTimeScalar { get; set; }

    PacketClientGameSettings CreateClientGameSettings();

    /// <summary>
    /// Returns null when no message of the day is set.
    /// </summary>
    PacketMOTD? CreateMotd();
}
