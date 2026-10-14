using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetUtilityFeedPacket : PetBasePacket, IDeserializable<PetUtilityFeedPacket>
{
    public new const byte OpCode = 19;

    public int PetId;

    public int Unknown2;

    public PetUtilityFeedPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetUtilityFeedPacket petUtilityFeedPacket)
    {
        petUtilityFeedPacket = new PetUtilityFeedPacket();

        var reader = new PacketReader(data);

        if (!petUtilityFeedPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petUtilityFeedPacket.PetId))
            return false;

        if (!reader.TryRead(out petUtilityFeedPacket.Unknown2))
            return false;

        return reader.RemainingLength == 0;
    }
}
