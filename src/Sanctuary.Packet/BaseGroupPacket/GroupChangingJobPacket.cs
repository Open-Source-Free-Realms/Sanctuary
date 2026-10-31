using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GroupChangingJobPacket : BaseGroupPacket, IDeserializable<GroupChangingJobPacket>
{
    public new const short OpCode = 14;

    public bool IsChangingJob;

    public GroupChangingJobPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupChangingJobPacket value)
    {
        value = new GroupChangingJobPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.IsChangingJob))
            return false;

        return reader.RemainingLength == 0;
    }
}
