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
    public const byte RequestStartGame = 0x05;
    public const byte Payload = 0x0E;
    public const byte Info = 0x10;
    public const byte StartGame = 0x11;
    public const byte GameOver = 0x12;
    public const byte LeaveGame = 0x13;
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

    /// <summary>
    /// Writes the minigame header: opcode, sub-op, then the stage id (the client routes a running game's
    /// packets by it), the sub-op again as an int (always -1) and the stage group id.
    /// </summary>
    public static void WriteHeader(PacketWriter writer, byte subOpCode, int stageId, int stageGroupId)
    {
        writer.Write(OpCode);
        writer.Write(subOpCode);
        writer.Write(stageId);
        writer.Write(-1);
        writer.Write(stageGroupId);
    }

    /// <summary>
    /// Reads the minigame header the client sends.
    /// </summary>
    public static bool TryReadHeader(ref PacketReader reader, out byte subOpCode, out int stageId, out int stageGroupId)
    {
        subOpCode = 0;
        stageId = 0;
        stageGroupId = 0;

        return reader.TryRead(out short opCode) && opCode == OpCode
            && reader.TryRead(out subOpCode)
            && reader.TryRead(out stageId)
            && reader.TryRead(out int _)
            && reader.TryRead(out stageGroupId);
    }
}

/// <summary>
/// A minigame packet with no body beyond the header (start game, leave game).
/// </summary>
public class MiniGameHeaderPacket : ISerializablePacket
{
    public byte SubOpCode;
    public int StageId;
    public int StageGroupId = -1;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        BaseMiniGamePacket.WriteHeader(writer, SubOpCode, StageId, StageGroupId);

        return writer.Buffer;
    }
}

/// <summary>
/// A message to or from a Flash minigame (MiniGameFlash:OnMessage): tab-separated, the message name first.
/// </summary>
public class MiniGamePayloadPacket : ISerializablePacket
{
    public int StageId;
    public int StageGroupId = -1;
    public string Payload = string.Empty;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        BaseMiniGamePacket.WriteHeader(writer, BaseMiniGamePacket.Payload, StageId, StageGroupId);

        // The client's payloads count a terminating null in their length, and the movie misreads the last
        // field of a message without one.
        writer.Write(Payload + '\0');

        return writer.Buffer;
    }
}

/// <summary>
/// Ends a minigame on the client (MiniGame:OnGameOver), which closes its window. Layout from Clone Wars
/// Adventures' EndActiveMinigame.
/// </summary>
public class MiniGameGameOverPacket : ISerializablePacket
{
    public int StageId;
    public int StageGroupId = -1;
    public bool Won;
    public int Unknown2;
    public int Unknown3;
    public int Unknown4;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        BaseMiniGamePacket.WriteHeader(writer, BaseMiniGamePacket.GameOver, StageId, StageGroupId);
        writer.Write(Won);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);

        return writer.Buffer;
    }
}

/// <summary>
/// Starts a minigame on the client (fires its MiniGame:JoinGame). Layout from the client's readers: the
/// minigame header then the body.
/// Field meanings follow Clone Wars Adventures' CreateActiveMinigame where the client doesn't name them.
/// </summary>
public class MiniGameInfoPacket : ISerializablePacket
{
    public int StageId;
    public int StageGroupId = -1;

    public int NameId;
    public int IconId;
    public int DescriptionId;
    public int Difficulty;
    public int BattleClassType;
    public int PortalEntryId;
    public bool Unknown4;
    public RewardBundleBase RewardBundle1 = new() { Success = false };
    public RewardBundleBase RewardBundle2 = new() { Success = false };
    public RewardBundleBase RewardBundle3 = new() { Success = false };
    public int RewardBundleListCount;
    /// <summary>
    /// The client reads one more int after the list's count, even when it is empty.
    /// </summary>
    public int RewardBundleListUnknown;
    public bool Unknown9;
    public bool Unknown10;
    public bool Unknown11;
    public bool Unknown12;
    public bool ShowEndScoreScreen;
    public string Unknown14 = string.Empty;
    public int Unknown15;
    public bool Unknown16;
    public int StageDefinitionId;
    public bool Unknown18;
    public bool Unknown19;
    public bool Unknown20;
    public bool Unknown21;
    public int Unknown22;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        BaseMiniGamePacket.WriteHeader(writer, BaseMiniGamePacket.Info, StageId, StageGroupId);

        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(DescriptionId);
        writer.Write(Difficulty);
        writer.Write(BattleClassType);
        writer.Write(PortalEntryId);
        writer.Write(Unknown4);
        RewardBundle1.Serialize(writer);
        RewardBundle2.Serialize(writer);
        RewardBundle3.Serialize(writer);
        writer.Write(RewardBundleListCount);
        writer.Write(RewardBundleListUnknown);
        writer.Write(Unknown9);
        writer.Write(Unknown10);
        writer.Write(Unknown11);
        writer.Write(Unknown12);
        writer.Write(ShowEndScoreScreen);
        writer.Write(Unknown14);
        writer.Write(Unknown15);
        writer.Write(Unknown16);
        writer.Write(StageDefinitionId);
        writer.Write(Unknown18);
        writer.Write(Unknown19);
        writer.Write(Unknown20);
        writer.Write(Unknown21);
        writer.Write(Unknown22);

        return writer.Buffer;
    }
}
