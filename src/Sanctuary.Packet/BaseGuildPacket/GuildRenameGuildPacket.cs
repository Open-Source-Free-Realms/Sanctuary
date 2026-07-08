using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildRenameGuildPacket : BaseGuildPacket, IDeserializable<GuildRenameGuildPacket>
{
    public new const short OpCode = 13;

    public ulong Guid;
    public string? Name;

    public GuildRenameGuildPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildRenameGuildPacket value)
    {
        if (TryDeserializeGuidName(data, out value))
            return true;

        return TryDeserializeNameGuid(data, out value);
    }

    private static bool TryDeserializeGuidName(ReadOnlySpan<byte> data, out GuildRenameGuildPacket value)
    {
        value = new GuildRenameGuildPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Name))
            return false;

        return reader.RemainingLength == 0;
    }

    private static bool TryDeserializeNameGuid(ReadOnlySpan<byte> data, out GuildRenameGuildPacket value)
    {
        value = new GuildRenameGuildPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Name))
            return false;

        value.Guid = 0;

        if (reader.RemainingLength == 0)
            return true;

        return reader.TryRead(out value.Guid);
    }
}
