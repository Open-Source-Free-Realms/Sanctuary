using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildInviteDeclinePacket : BaseGuildPacket, IDeserializable<GuildInviteDeclinePacket>
{
    public new const short OpCode = 9;

    public ulong PlayerGuid;

    public GuildInviteDeclinePacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildInviteDeclinePacket value)
    {
        value = new GuildInviteDeclinePacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PlayerGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}