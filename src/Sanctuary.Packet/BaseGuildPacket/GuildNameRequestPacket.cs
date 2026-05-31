using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildNameRequestPacket : BaseGuildPacket, IDeserializable<GuildNameRequestPacket>
{
    public new const short OpCode = 14;

    public ulong Guid;

    public GuildNameRequestPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildNameRequestPacket value)
    {
        value = new GuildNameRequestPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}