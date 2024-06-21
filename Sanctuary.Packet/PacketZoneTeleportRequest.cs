using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketZoneTeleportRequest : IDeserializable<PacketZoneTeleportRequest>
{
    public const short OpCode = 90;

    public int Id;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketZoneTeleportRequest value)
    {
        value = new PacketZoneTeleportRequest();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Id))
            return false;

        return reader.RemainingLength == 0;
    }
}