using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PacketClientMetrics : IDeserializable<PacketClientMetrics>
{
    public const short OpCode = 105;

    public ClientMetrics Metrics = new();

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketClientMetrics value)
    {
        value = new PacketClientMetrics();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!value.Metrics.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}