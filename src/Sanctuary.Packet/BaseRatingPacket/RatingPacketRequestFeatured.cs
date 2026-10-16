using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class RatingPacketRequestFeatured : BaseRatingPacket, IDeserializable<RatingPacketRequestFeatured>
{
    public new const byte OpCode = 20;

    public string System = string.Empty;

    public ulong Unknown;

    public RatingPacketRequestFeatured() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketRequestFeatured value)
    {
        value = new RatingPacketRequestFeatured();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.System))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}