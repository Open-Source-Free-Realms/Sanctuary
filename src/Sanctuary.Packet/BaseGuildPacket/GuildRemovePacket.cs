using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildRemovePacket : BaseGuildPacket, IDeserializable<GuildRemovePacket>
{
    public new const short OpCode = 6;

    public ulong PlayerGuid;
    public ulong GuildGuid;

    public GuildRemovePacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildRemovePacket value)
    {
        value = new GuildRemovePacket();

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