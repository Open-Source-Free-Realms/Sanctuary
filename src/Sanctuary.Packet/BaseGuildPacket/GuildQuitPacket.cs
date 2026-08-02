using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildQuitPacket : BaseGuildPacket, IDeserializable<GuildQuitPacket>
{
    public new const short OpCode = 7;

    public ulong Guid;

    public GuildQuitPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildQuitPacket value)
    {
        value = new GuildQuitPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}