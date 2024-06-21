using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ChangeNameRequestPacket : BaseNameChangePacket, IDeserializable<ChangeNameRequestPacket>
{
    public new const short OpCode = 3;

    public NameChangeType Type;

    public ulong Guid;

    public NameData Name = new();

    public ChangeNameRequestPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ChangeNameRequestPacket value)
    {
        value = new ChangeNameRequestPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Type))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!value.Name.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}