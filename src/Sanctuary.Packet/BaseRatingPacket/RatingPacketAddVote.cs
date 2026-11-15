using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class RatingPacketAddVote : BaseRatingPacket, IDeserializable<RatingPacketAddVote>
{
    public new const byte OpCode = 8;

    public string System = string.Empty;
    public string HouseGuid = string.Empty;
    public string Category = string.Empty;
    public string PlayerGuid = string.Empty;

    public float Rating;

    public RatingPacketAddVote() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketAddVote value)
    {
        value = new RatingPacketAddVote();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.System))
            return false;

        if (!reader.TryRead(out value.HouseGuid))
            return false;

        if (!reader.TryRead(out value.Category))
            return false;

        if (!reader.TryRead(out value.PlayerGuid))
            return false;

        if (!reader.TryRead(out value.Rating))
            return false;

        return reader.RemainingLength == 0;
    }
}