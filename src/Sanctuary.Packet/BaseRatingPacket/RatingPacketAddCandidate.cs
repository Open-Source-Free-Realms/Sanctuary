using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class RatingPacketAddCandidate : BaseRatingPacket, IDeserializable<RatingPacketAddCandidate>
{
    public new const byte OpCode = 6;

    public string System = string.Empty;
    public string InstanceGuid = string.Empty;
    public string Unknown = string.Empty;
    public string HouseName = string.Empty;

    public ulong OwnerGuid;

    public string Unknown2 = string.Empty;
    public string Description = string.Empty;
    public string Keywords = string.Empty;

    public RatingPacketAddCandidate() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketAddCandidate value)
    {
        value = new RatingPacketAddCandidate();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.System))
            return false;

        if (!reader.TryRead(out value.InstanceGuid))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.HouseName))
            return false;

        if (!reader.TryRead(out value.OwnerGuid))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        if (!reader.TryRead(out value.Description))
            return false;

        if (!reader.TryRead(out value.Keywords))
            return false;

        return reader.RemainingLength == 0;
    }
}