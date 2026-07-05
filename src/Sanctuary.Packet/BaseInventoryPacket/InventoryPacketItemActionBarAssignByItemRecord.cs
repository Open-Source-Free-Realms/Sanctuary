using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class InventoryPacketItemActionBarAssignByItemRecord : BaseInventoryPacket, IDeserializable<InventoryPacketItemActionBarAssignByItemRecord>
{
    public new const short OpCode = 9;

    public int Slot;
    public ItemRecord Item = new();

    public InventoryPacketItemActionBarAssignByItemRecord() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out InventoryPacketItemActionBarAssignByItemRecord value)
    {
        value = new InventoryPacketItemActionBarAssignByItemRecord();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Slot))
            return false;

        if (!value.Item.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
