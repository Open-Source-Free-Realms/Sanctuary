using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketGameTimeSync : ISerializablePacket, IDeserializable<PacketGameTimeSync>
{
    public const short OpCode = 52;

    public long Time;

    public int ServerRate;

    public bool UseClientTime;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Time);
        writer.Write(ServerRate);
        writer.Write(UseClientTime);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketGameTimeSync value)
    {
        value = new PacketGameTimeSync();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Time))
            return false;

        if (!reader.TryRead(out value.ServerRate))
            return false;

        if (!reader.TryRead(out value.UseClientTime))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"{nameof(Time)}: {Time}, {nameof(ServerRate)}: {ServerRate}, {nameof(UseClientTime)}: {UseClientTime}";
    }
}