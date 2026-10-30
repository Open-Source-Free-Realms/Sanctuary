using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class TaskAddPacket : BaseUiPacket, ISerializablePacket, IDeserializable<TaskAddPacket>
{
    public new const byte OpCode = 1;

    public int TaskId;

    public int Unknown;

    public int Unknown2;

    public bool Unknown3;

    public bool Unknown4;

    public int Unknown5;

    public bool Unknown6;

    public int Unknown7;

    public TaskAddPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TaskId);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Unknown7);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out TaskAddPacket taskAddPacket)
    {
        taskAddPacket = new TaskAddPacket();

        var reader = new PacketReader(data);

        if (!taskAddPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out taskAddPacket.TaskId))
            return false;

        if (!reader.TryRead(out taskAddPacket.Unknown))
            return false;

        if (!reader.TryRead(out taskAddPacket.Unknown2))
            return false;

        if (!reader.TryRead(out taskAddPacket.Unknown3))
            return false;

        if (!reader.TryRead(out taskAddPacket.Unknown4))
            return false;

        if (!reader.TryRead(out taskAddPacket.Unknown5))
            return false;

        if (!reader.TryRead(out taskAddPacket.Unknown6))
            return false;

        if (!reader.TryRead(out taskAddPacket.Unknown7))
            return false;

        return reader.RemainingLength == 0;
    }
}
