using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class InventoryPacketEquipByGuid : BaseInventoryPacket, IDeserializable<InventoryPacketEquipByGuid>
{
    public new const short OpCode = 3;

    public int Guid;
    public int ProfileId;
    public int Slot;

    public InventoryPacketEquipByGuid() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out InventoryPacketEquipByGuid value)
    {
        value = new InventoryPacketEquipByGuid();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.ProfileId))
            return false;

        if (!reader.TryRead(out value.Slot))
            return false;

        return reader.RemainingLength == 0;
    }
}