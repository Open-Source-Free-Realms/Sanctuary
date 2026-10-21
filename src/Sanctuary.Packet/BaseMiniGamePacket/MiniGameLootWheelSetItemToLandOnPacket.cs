using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class MiniGameLootWheelSetItemToLandOnPacket : BaseMiniGamePacket, ISerializablePacket, IDeserializable<MiniGameLootWheelSetItemToLandOnPacket>
{
    public new const byte OpCode = 45;

    public RewardBundleInfo RewardBundle = new();

    public MiniGameLootWheelSetItemToLandOnPacket() : base(OpCode)
    {
    }

    public new byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(StateId);
        writer.Write(GameId);
        writer.Write(GroupId);

        RewardBundle.Serialize(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MiniGameLootWheelSetItemToLandOnPacket miniGameLootWheelSetItemToLandOnPacket)
    {
        miniGameLootWheelSetItemToLandOnPacket = new();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != BaseMiniGamePacket.OpCode)
            return false;

        if (!reader.TryRead(out byte subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out miniGameLootWheelSetItemToLandOnPacket.StateId))
            return false;

        if (!reader.TryRead(out miniGameLootWheelSetItemToLandOnPacket.GameId))
            return false;

        if (!reader.TryRead(out miniGameLootWheelSetItemToLandOnPacket.GroupId))
            return false;

        if (!miniGameLootWheelSetItemToLandOnPacket.RewardBundle.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
