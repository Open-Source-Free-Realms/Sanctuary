using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetEquipPacket : PetBasePacket, ISerializablePacket, IDeserializable<PetEquipPacket>
{
    public new const byte OpCode = 11;

    public bool Unequip;

    public int Slot;

    public int ItemGuid;

    public PetEquipPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unequip);
        writer.Write(Slot);
        writer.Write(ItemGuid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetEquipPacket petEquipPacket)
    {
        petEquipPacket = new PetEquipPacket();

        var reader = new PacketReader(data);

        if (!petEquipPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petEquipPacket.Unequip))
            return false;

        if (!reader.TryRead(out petEquipPacket.Slot))
            return false;

        if (!reader.TryRead(out petEquipPacket.ItemGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}
