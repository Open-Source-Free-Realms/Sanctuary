using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetSummonRecallPacket : PetBasePacket, IDeserializable<PetSummonRecallPacket>
{
    public new const byte OpCode = 4;

    public int PetId;

    public bool Summon;

    public PetSummonRecallPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetSummonRecallPacket petSummonRecallPacket)
    {
        petSummonRecallPacket = new PetSummonRecallPacket();

        var reader = new PacketReader(data);

        if (!petSummonRecallPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petSummonRecallPacket.PetId))
            return false;

        if (!reader.TryRead(out petSummonRecallPacket.Summon))
            return false;

        return reader.RemainingLength == 0;
    }
}
