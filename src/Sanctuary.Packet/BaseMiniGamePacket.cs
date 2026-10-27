namespace Sanctuary.Packet;

/// <summary>
/// Minigame packets (the client's BaseMiniGamePacket family). Sub-ops seen in the client's packet constructors;
/// the layouts follow the sister project for Clone Wars Adventures (same engine), whose sub-ops are one higher
/// (https://github.com/soir20/oxide, src/game_server/packets/minigame.rs).
/// </summary>
public static class BaseMiniGamePacket
{
    public const short OpCode = 0x27;

    public const byte DataLocked = 0x01;
    public const byte DataUnlocked = 0x02;
    public const byte Payload = 0x0E;
    public const byte Info = 0x10;
    public const byte GameOver = 0x12;
    public const byte UpdateGameTimeScalar = 0x14;
    public const byte StarsEarned = 0x15;
    public const byte KnockOut = 0x17;
    public const byte StoreAdvertisement = 0x2C;
    public const byte LootWheelSetItemToLandOn = 0x2D;
    public const byte LootWheelOnRotationStopped = 0x2E;
    public const byte GameEndScore = 0x2F;
    public const byte GroupInfo = 0x32;
    public const byte CreateGameResult = 0x43;
    public const byte UpdateReward = 0x44;
}
