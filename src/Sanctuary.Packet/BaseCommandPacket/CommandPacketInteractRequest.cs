using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketInteractRequest : BaseCommandPacket, IDeserializable<CommandPacketInteractRequest>
{
    public new const short OpCode = 8;

    public ulong Guid;

    public bool Unknown;

    public CommandPacketInteractRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketInteractRequest value)
    {
        value = new CommandPacketInteractRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}