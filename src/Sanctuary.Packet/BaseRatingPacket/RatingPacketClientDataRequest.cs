using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class RatingPacketClientDataRequest : BaseRatingPacket, IDeserializable<RatingPacketClientDataRequest>
{
    public new const byte OpCode = 3;

    public string System = string.Empty;

    public RatingListType ListType;

    public Dictionary<uint, string> HouseGuids = [];

    public RatingPacketClientDataRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketClientDataRequest value)
    {
        value = new RatingPacketClientDataRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.System))
            return false;

        if (!reader.TryRead(out value.ListType))
            return false;

        if (!reader.TryRead(out int houseGuidCount))
            return false;

        for (var i = 0; i < houseGuidCount; i++)
        {
            if (!reader.TryRead(out uint key))
                return false;

            if (!reader.TryRead(out string houseGuid))
                return false;

            value.HouseGuids[key] = houseGuid;
        }

        return reader.RemainingLength == 0;
    }
}