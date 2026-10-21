using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ActivityPacketJoinActivityRequest : BaseActivityPacket, ISerializablePacket, IDeserializable<ActivityPacketJoinActivityRequest>
{
    public new const byte OpCode = 2;

    public int ActivityId;
    public int Unknown;

    public ActivityPacketJoinActivityRequest() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ActivityId);
        writer.Write(Unknown);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ActivityPacketJoinActivityRequest activityPacketJoinActivityRequest)
    {
        activityPacketJoinActivityRequest = new ActivityPacketJoinActivityRequest();

        var reader = new PacketReader(data);

        if (!activityPacketJoinActivityRequest.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out activityPacketJoinActivityRequest.ActivityId))
            return false;

        if (!reader.TryRead(out activityPacketJoinActivityRequest.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}
