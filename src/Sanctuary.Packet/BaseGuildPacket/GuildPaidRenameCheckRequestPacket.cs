using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildPaidRenameCheckRequestPacket : BaseGuildPacket, IDeserializable<GuildPaidRenameCheckRequestPacket>
{
    public new const short OpCode = 16;

    public ulong Guid;
    public string? Name;

    public GuildPaidRenameCheckRequestPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GuildPaidRenameCheckRequestPacket value)
    {
        value = new GuildPaidRenameCheckRequestPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Name))
            return false;

        return reader.RemainingLength == 0;
    }
}