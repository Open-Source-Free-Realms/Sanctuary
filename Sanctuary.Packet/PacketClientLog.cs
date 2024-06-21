using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketClientLog : IDeserializable<PacketClientLog>
{
    public const short OpCode = 109;

    public string? Filename;

    public string? Message;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketClientLog value)
    {
        value = new PacketClientLog();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Filename))
            return false;

        if (!reader.TryRead(out value.Message))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"{nameof(Filename)}: {Filename}, {nameof(Message)}: {Message}";
    }
}