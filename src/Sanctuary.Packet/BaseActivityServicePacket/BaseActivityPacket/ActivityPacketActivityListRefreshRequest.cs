using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ActivityPacketActivityListRefreshRequest : BaseActivityPacket, IDeserializable<ActivityPacketActivityListRefreshRequest>
{
    public new const byte OpCode = 4;

    public ActivityPacketActivityListRefreshRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ActivityPacketActivityListRefreshRequest value)
    {
        value = new ActivityPacketActivityListRefreshRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}