using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetInWaterPacket : PetBasePacket, IDeserializable<PetInWaterPacket>
{
    public new const byte OpCode = 28;

    public bool InWater;

    public PetInWaterPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetInWaterPacket petInWaterPacket)
    {
        petInWaterPacket = new PetInWaterPacket();

        var reader = new PacketReader(data);

        if (!petInWaterPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petInWaterPacket.InWater))
            return false;

        return reader.RemainingLength == 0;
    }
}
