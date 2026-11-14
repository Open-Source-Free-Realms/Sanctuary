using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class InventoryPacketUseImmediateActivationItem : BaseInventoryPacket, IDeserializable<InventoryPacketUseImmediateActivationItem>
{
    public new const short OpCode = 15;

    public int ItemGuid;

    public InventoryPacketUseImmediateActivationItem() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out InventoryPacketUseImmediateActivationItem value)
    {
        value = new InventoryPacketUseImmediateActivationItem();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.ItemGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}