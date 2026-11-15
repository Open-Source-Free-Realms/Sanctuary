using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class TaskAddPacket : BaseUiPacket, ISerializablePacket, IDeserializable<TaskAddPacket>
{
    public new const byte OpCode = 1;

    public int TaskId;

    public int IconId;

    public int NameId;

    public bool IsBonus;

    public bool MembersOnly;

    public int Category;

    public bool Unknown6;

    public int RequiredCount;

    public TaskAddPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TaskId);
        writer.Write(IconId);
        writer.Write(NameId);
        writer.Write(IsBonus);
        writer.Write(MembersOnly);
        writer.Write(Category);
        writer.Write(Unknown6);
        writer.Write(RequiredCount);

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

        if (!reader.TryRead(out taskAddPacket.IconId))
            return false;

        if (!reader.TryRead(out taskAddPacket.NameId))
            return false;

        if (!reader.TryRead(out taskAddPacket.IsBonus))
            return false;

        if (!reader.TryRead(out taskAddPacket.MembersOnly))
            return false;

        if (!reader.TryRead(out taskAddPacket.Category))
            return false;

        if (!reader.TryRead(out taskAddPacket.Unknown6))
            return false;

        if (!reader.TryRead(out taskAddPacket.RequiredCount))
            return false;

        return reader.RemainingLength == 0;
    }
}
