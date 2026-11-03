using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketEndDialog : BaseCommandPacket, ISerializablePacket, IDeserializable<CommandPacketEndDialog>
{
    public new const short OpCode = 4;

    public CommandPacketEndDialog() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketEndDialog value)
    {
        value = new CommandPacketEndDialog();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
