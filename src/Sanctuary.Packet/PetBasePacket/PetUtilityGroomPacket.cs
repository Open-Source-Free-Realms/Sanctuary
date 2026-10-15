using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetUtilityGroomPacket : PetBasePacket, IDeserializable<PetUtilityGroomPacket>
{
    public new const byte OpCode = 18;

    public int PetId;

    public int Unknown2;

    public PetUtilityGroomPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetUtilityGroomPacket petUtilityGroomPacket)
    {
        petUtilityGroomPacket = new PetUtilityGroomPacket();

        var reader = new PacketReader(data);

        if (!petUtilityGroomPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petUtilityGroomPacket.PetId))
            return false;

        if (!reader.TryRead(out petUtilityGroomPacket.Unknown2))
            return false;

        return reader.RemainingLength == 0;
    }
}
