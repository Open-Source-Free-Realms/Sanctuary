using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class RatingPacketSearchRequest : BaseRatingPacket, IDeserializable<RatingPacketSearchRequest>
{
    public new const byte OpCode = 12;

    public ulong PlayerGuid;

    public string System = string.Empty;
    public string SearchText = string.Empty;

    public RatingPacketSearchRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketSearchRequest value)
    {
        value = new RatingPacketSearchRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PlayerGuid))
            return false;

        if (!reader.TryRead(out value.System))
            return false;

        if (!reader.TryRead(out value.SearchText))
            return false;

        return reader.RemainingLength == 0;
    }
}