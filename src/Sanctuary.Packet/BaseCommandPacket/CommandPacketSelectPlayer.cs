using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketSelectPlayer : BaseCommandPacket, IDeserializable<CommandPacketSelectPlayer>
{
    public new const short OpCode = 19;

    public ulong Guid;
    public ulong SelectGuid;

    public CommandPacketSelectPlayer() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketSelectPlayer value)
    {
        value = new CommandPacketSelectPlayer();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.SelectGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}