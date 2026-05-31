using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildDemotePacket : BaseGuildPacket, IDeserializable<GuildDemotePacket>
{
    public new const short OpCode = 5;

    public ulong PlayerGuid;
    public ulong GuildGuid;

    public GuildDemotePacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildDemotePacket value)
    {
        value = new GuildDemotePacket();

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