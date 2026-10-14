using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetPerformTrickPacket : PetBasePacket, IDeserializable<PetPerformTrickPacket>
{
    public new const byte OpCode = 7;

    public int TrickId;

    public PetPerformTrickPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetPerformTrickPacket petPerformTrickPacket)
    {
        petPerformTrickPacket = new PetPerformTrickPacket();

        var reader = new PacketReader(data);

        if (!petPerformTrickPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petPerformTrickPacket.TrickId))
            return false;

        return reader.RemainingLength == 0;
    }
}
