using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketSetProfile : BaseCommandPacket, IDeserializable<CommandPacketSetProfile>
{
    public new const short OpCode = 13;

    public int Id;

    public CommandPacketSetProfile() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketSetProfile value)
    {
        value = new CommandPacketSetProfile();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Id))
            return false;

        return reader.RemainingLength == 0;
    }
}