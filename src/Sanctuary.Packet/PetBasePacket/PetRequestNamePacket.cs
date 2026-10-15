using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetRequestNamePacket : PetBasePacket, IDeserializable<PetRequestNamePacket>
{
    public new const byte OpCode = 6;

    public string Name = string.Empty;

    public PetRequestNamePacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetRequestNamePacket petRequestNamePacket)
    {
        petRequestNamePacket = new PetRequestNamePacket();

        var reader = new PacketReader(data);

        if (!petRequestNamePacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petRequestNamePacket.Name))
            return false;

        return reader.RemainingLength == 0;
    }
}
