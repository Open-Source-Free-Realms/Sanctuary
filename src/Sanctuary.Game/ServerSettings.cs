using Sanctuary.Packet;

namespace Sanctuary.Game;

public class ServerSettings : IServerSettings
{
    public bool IsLocked { get; set; }

    public string? MotdTitle { get; set; }
    public string? MotdMessage { get; set; }

    public float GameTimeScalar { get; set; } = 1.0f;

    public PacketClientGameSettings CreateClientGameSettings()
    {
        return new PacketClientGameSettings
        {
            Unknown = 4,
            Unknown2 = 7,
            PowerHourEffectTag = 268,
            Unknown4 = true,
            GameTimeScalar = GameTimeScalar
        };
    }

    public PacketMOTD? CreateMotd()
    {
        if (string.IsNullOrEmpty(MotdMessage))
            return null;

        return new PacketMOTD
        {
            Title = MotdTitle ?? "Message of the Day",
            Message = MotdMessage
        };
    }
}
