using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

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

/// <summary>
/// Starts a minigame on the client (fires its MiniGame:JoinGame). Layout from the client's readers: the
/// minigame header (short 0x27, byte sub-op, three ints; the second is passed on as the game) then the body.
/// Field meanings follow Clone Wars Adventures' CreateActiveMinigame where the client doesn't name them.
/// </summary>
public class MiniGameInfoPacket : ISerializablePacket
{
    public int HeaderUnknown;
    public int MinigameId;
    public int StageGroupId = -1;

    public int NameId;
    public int IconId;
    public int DescriptionId;
    public bool Unknown4;
    public RewardBundleBase RewardBundle1 = new() { Success = false };
    public RewardBundleBase RewardBundle2 = new() { Success = false };
    public RewardBundleBase RewardBundle3 = new() { Success = false };
    public int RewardBundleListCount;
    public bool Unknown9;
    public bool Unknown10;
    public bool Unknown11;
    public bool Unknown12;
    public bool ShowEndScoreScreen;
    public string Unknown14 = string.Empty;
    public int Unknown15;
    public bool Unknown16;
    public int StageId;
    public bool Unknown18;
    public bool Unknown19;
    public bool Unknown20;
    public bool Unknown21;
    public int Unknown22;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(BaseMiniGamePacket.OpCode);
        writer.Write(BaseMiniGamePacket.Info);
        writer.Write(HeaderUnknown);
        writer.Write(MinigameId);
        writer.Write(StageGroupId);

        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(DescriptionId);
        writer.Write(Unknown4);
        RewardBundle1.Serialize(writer);
        RewardBundle2.Serialize(writer);
        RewardBundle3.Serialize(writer);
        writer.Write(RewardBundleListCount);
        writer.Write(Unknown9);
        writer.Write(Unknown10);
        writer.Write(Unknown11);
        writer.Write(Unknown12);
        writer.Write(ShowEndScoreScreen);
        writer.Write(Unknown14);
        writer.Write(Unknown15);
        writer.Write(Unknown16);
        writer.Write(StageId);
        writer.Write(Unknown18);
        writer.Write(Unknown19);
        writer.Write(Unknown20);
        writer.Write(Unknown21);
        writer.Write(Unknown22);

        return writer.Buffer;
    }
}
