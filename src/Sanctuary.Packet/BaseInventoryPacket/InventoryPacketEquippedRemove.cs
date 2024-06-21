using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class InventoryPacketEquippedRemove : BaseInventoryPacket, IDeserializable<InventoryPacketEquippedRemove>
{
    public new const short OpCode = 2;

    public int Slot;
    public int ProfileId;

    public InventoryPacketEquippedRemove() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out InventoryPacketEquippedRemove value)
    {
        value = new InventoryPacketEquippedRemove();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Slot))
            return false;

        if (!reader.TryRead(out value.ProfileId))
            return false;

        return reader.RemainingLength == 0;
    }
}