using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ListQueuesRequestPacket : BaseMatchmakingPacket, IDeserializable<ListQueuesRequestPacket>
{
    public new const short OpCode = 1;

    public ulong Guid;

    public ListQueuesRequestPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ListQueuesRequestPacket value)
    {
        value = new ListQueuesRequestPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}