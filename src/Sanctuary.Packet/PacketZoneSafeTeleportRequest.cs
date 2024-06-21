using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketZoneSafeTeleportRequest : IDeserializable<PacketZoneSafeTeleportRequest>
{
    public const short OpCode = 122;

    public int Id;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketZoneSafeTeleportRequest value)
    {
        value = new PacketZoneSafeTeleportRequest();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        return reader.RemainingLength == 0;
    }
}