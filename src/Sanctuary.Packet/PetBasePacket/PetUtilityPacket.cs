using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetUtilityPacket : PetBasePacket, ISerializablePacket, IDeserializable<PetUtilityPacket>
{
    public new const byte OpCode = 16;

    public bool Unequip;

    public int PetId;

    public int Slot;

    public int ItemGuid;

    public PetUtilityPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unequip);
        writer.Write(PetId);
        writer.Write(Slot);
        writer.Write(ItemGuid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetUtilityPacket petUtilityPacket)
    {
        petUtilityPacket = new PetUtilityPacket();

        var reader = new PacketReader(data);

        if (!petUtilityPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petUtilityPacket.Unequip))
            return false;

        if (!reader.TryRead(out petUtilityPacket.PetId))
            return false;

        if (!reader.TryRead(out petUtilityPacket.Slot))
            return false;

        if (!reader.TryRead(out petUtilityPacket.ItemGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}
