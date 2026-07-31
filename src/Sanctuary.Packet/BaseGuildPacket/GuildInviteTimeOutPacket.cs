using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildInviteTimeOutPacket : BaseGuildPacket, IDeserializable<GuildInviteTimeOutPacket>
{
    public new const short OpCode = 10;

    public ulong PlayerGuid;

    public GuildInviteTimeOutPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildInviteTimeOutPacket value)
    {
        value = new GuildInviteTimeOutPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PlayerGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}