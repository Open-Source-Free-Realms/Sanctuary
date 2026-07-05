using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildInviteAcceptPacket : BaseGuildPacket, IDeserializable<GuildInviteAcceptPacket>
{
    public new const short OpCode = 8;

    public ulong PlayerGuid;

    public GuildInviteAcceptPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildInviteAcceptPacket value)
    {
        value = new GuildInviteAcceptPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PlayerGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}