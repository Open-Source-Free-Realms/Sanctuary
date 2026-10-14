using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetTrickFavoriteSetPacket : PetBasePacket, ISerializablePacket, IDeserializable<PetTrickFavoriteSetPacket>
{
    public new const byte OpCode = 14;

    public int PetId;

    public int Slot;

    public int TrickId;

    public PetTrickFavoriteSetPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PetId);
        writer.Write(Slot);
        writer.Write(TrickId);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetTrickFavoriteSetPacket petTrickFavoriteSetPacket)
    {
        petTrickFavoriteSetPacket = new PetTrickFavoriteSetPacket();

        var reader = new PacketReader(data);

        if (!petTrickFavoriteSetPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petTrickFavoriteSetPacket.PetId))
            return false;

        if (!reader.TryRead(out petTrickFavoriteSetPacket.Slot))
            return false;

        if (!reader.TryRead(out petTrickFavoriteSetPacket.TrickId))
            return false;

        return reader.RemainingLength == 0;
    }
}
