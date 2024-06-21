using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class CheckNamePacket : BaseNameChangePacket, IDeserializable<CheckNamePacket>
{
    public new const short OpCode = 1;

    public NameChangeType Type;

    public ulong Guid;

    public NameData Name = new();

    public bool Token;

    public CheckNamePacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CheckNamePacket value)
    {
        value = new CheckNamePacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Type))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!value.Name.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Token))
            return false;

        return reader.RemainingLength == 0;
    }
}