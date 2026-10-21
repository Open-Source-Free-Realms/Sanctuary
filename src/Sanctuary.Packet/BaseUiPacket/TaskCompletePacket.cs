using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class TaskCompletePacket : BaseUiPacket, ISerializablePacket, IDeserializable<TaskCompletePacket>
{
    public new const byte OpCode = 3;

    public int TaskId;

    public TaskCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TaskId);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out TaskCompletePacket taskCompletePacket)
    {
        taskCompletePacket = new TaskCompletePacket();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != BaseUiPacket.OpCode)
            return false;

        if (!reader.TryRead(out byte subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out taskCompletePacket.TaskId))
            return false;

        return reader.RemainingLength == 0;
    }
}
