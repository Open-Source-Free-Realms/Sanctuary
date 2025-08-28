using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketInteractionSelect : BaseCommandPacket, IDeserializable<CommandPacketInteractionSelect>
{
    public new const short OpCode = 10;

    public ulong Guid;

    public int Id;

    public CommandPacketInteractionSelect() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketInteractionSelect value)
    {
        value = new CommandPacketInteractionSelect();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Id))
            return false;

        return reader.RemainingLength == 0;
    }
}