using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ActivityPacketJoinActivityRequest : BaseActivityPacket, IDeserializable<ActivityPacketJoinActivityRequest>
{
    public new const byte OpCode = 2;

    public int ActivityId;
    public int TimezoneOffset;

    public ActivityPacketJoinActivityRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ActivityPacketJoinActivityRequest value)
    {
        value = new ActivityPacketJoinActivityRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.ActivityId))
            return false;

        if (!reader.TryRead(out value.TimezoneOffset))
            return false;

        return reader.RemainingLength == 0;
    }
}