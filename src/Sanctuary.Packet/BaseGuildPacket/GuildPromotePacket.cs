using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildPromotePacket : BaseGuildPacket, IDeserializable<GuildPromotePacket>
{
    public new const short OpCode = 4;

    public ulong PlayerGuid;
    public ulong GuildGuid;

    public GuildPromotePacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildPromotePacket value)
    {
        value = new GuildPromotePacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PlayerGuid))
            return false;

        if (!reader.TryRead(out value.GuildGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}