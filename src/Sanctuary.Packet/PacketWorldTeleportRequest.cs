using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketWorldTeleportRequest : IDeserializable<PacketWorldTeleportRequest>
{
    public const short OpCode = 58;

    public ulong Guid;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketWorldTeleportRequest value)
    {
        value = new PacketWorldTeleportRequest();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}