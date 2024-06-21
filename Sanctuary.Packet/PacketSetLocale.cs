using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketSetLocale : IDeserializable<PacketSetLocale>
{
    public const short OpCode = 88;

    public string? Locale;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketSetLocale value)
    {
        value = new PacketSetLocale();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Locale))
            return false;

        return reader.RemainingLength == 0;
    }
}