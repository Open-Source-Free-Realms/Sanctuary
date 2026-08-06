using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildRenameGuildPacket : BaseGuildPacket, IDeserializable<GuildRenameGuildPacket>
{
    public new const short OpCode = 13;

    public ulong Guid;

    public string? Name;
    public string? Locale;

    /// <summary>
    /// True when the <see cref="ClientSettings.Environment"/> isn't equal to 1, which is the China Environment.
    /// </summary>
    public bool IsNonChinaEnvironment;

    public GuildRenameGuildPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildRenameGuildPacket value)
    {
        value = new GuildRenameGuildPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Name))
            return false;

        if (!reader.TryRead(out value.Locale))
            return false;

        if (!reader.TryRead(out value.IsNonChinaEnvironment))
            return false;

        return reader.RemainingLength == 0;
    }
}
