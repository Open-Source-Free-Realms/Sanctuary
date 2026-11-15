using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ActivityPacketActivityListRefreshRequest : BaseActivityPacket, ISerializablePacket, IDeserializable<ActivityPacketActivityListRefreshRequest>
{
    public new const byte OpCode = 4;

    public ActivityPacketActivityListRefreshRequest() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ActivityPacketActivityListRefreshRequest activityPacketActivityListRefreshRequest)
    {
        activityPacketActivityListRefreshRequest = new ActivityPacketActivityListRefreshRequest();

        var reader = new PacketReader(data);

        return activityPacketActivityListRefreshRequest.TryRead(ref reader) && reader.RemainingLength == 0;
    }
}
