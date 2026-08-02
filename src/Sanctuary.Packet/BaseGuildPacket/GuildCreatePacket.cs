using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildCreatePacket : BaseGuildPacket, IDeserializable<GuildCreatePacket>
{
    public new const short OpCode = 1;

    // No longer used
    public string? Name;

    public string? TemporaryName;
    public string? Locale;

    public GuildCreatePacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildCreatePacket value)
    {
        value = new GuildCreatePacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Name))
            return false;

        if (!reader.TryRead(out value.TemporaryName))
            return false;

        if (!reader.TryRead(out value.Locale))
            return false;

        return reader.RemainingLength == 0;
    }
}