using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class InventoryPacketCheckImmediateActivationItem : BaseInventoryPacket, IDeserializable<InventoryPacketCheckImmediateActivationItem>
{
    public new const short OpCode = 16;

    public int ItemGuid;

    public InventoryPacketCheckImmediateActivationItem() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out InventoryPacketCheckImmediateActivationItem value)
    {
        value = new InventoryPacketCheckImmediateActivationItem();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.ItemGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}