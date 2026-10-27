using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketClientLuaMetrics : IDeserializable<PacketClientLuaMetrics>
{
    public const short OpCode = 142;

    public class Metric : IDeserializableType
    {
        public uint Hash;
        public string Name = string.Empty;
        public int Value;

        public bool TryRead(ref PacketReader reader)
        {
            return reader.TryRead(out Hash)
                && reader.TryRead(out Name)
                && reader.TryRead(out Value);
        }
    }

    public List<Metric> Metrics = new();

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketClientLuaMetrics value)
    {
        value = new PacketClientLuaMetrics();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryReadList(out value.Metrics))
            return false;

        return reader.RemainingLength == 0;
    }
}
