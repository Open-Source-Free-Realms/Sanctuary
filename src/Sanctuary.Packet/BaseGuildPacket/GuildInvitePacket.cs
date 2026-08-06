using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildInvitePacket : BaseGuildPacket, IDeserializable<GuildInvitePacket>
{
    public new const short OpCode = 3;

    public ulong GuildGuid;

    public ulong PlayerGuid;
    public string? PlayerName;

    public GuildInvitePacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildInvitePacket value)
    {
        value = new GuildInvitePacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PlayerGuid))
            return false;

        if (!reader.TryRead(out value.GuildGuid))
            return false;

        if (!reader.TryRead(out value.PlayerName))
            return false;

        return reader.RemainingLength == 0;
    }
}