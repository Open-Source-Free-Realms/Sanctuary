using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class MiniGameLootWheelSetItemToLandOnPacket : BaseMiniGamePacket, ISerializablePacket, IDeserializable<MiniGameLootWheelSetItemToLandOnPacket>
{
    public new const byte OpCode = 45;

    public RewardBundleBase RewardBundle = RewardBundleBase.CreateEmpty();

    public MiniGameLootWheelSetItemToLandOnPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        RewardBundle.Serialize(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MiniGameLootWheelSetItemToLandOnPacket miniGameLootWheelSetItemToLandOnPacket)
    {
        miniGameLootWheelSetItemToLandOnPacket = new();

        var reader = new PacketReader(data);

        if (!miniGameLootWheelSetItemToLandOnPacket.TryRead(ref reader))
            return false;

        if (!miniGameLootWheelSetItemToLandOnPacket.RewardBundle.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
