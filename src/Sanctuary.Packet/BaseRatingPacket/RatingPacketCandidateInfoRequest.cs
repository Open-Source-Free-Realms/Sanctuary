using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class RatingPacketCandidateInfoRequest : BaseRatingPacket, IDeserializable<RatingPacketCandidateInfoRequest>
{
    public new const byte OpCode = 16;

    public string System = string.Empty;
    public string InstanceGuid = string.Empty;

    public ulong OwnerGuid;
    public ulong RequesterGuid;

    public RatingPacketCandidateInfoRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketCandidateInfoRequest value)
    {
        value = new RatingPacketCandidateInfoRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.System))
            return false;

        if (!reader.TryRead(out value.InstanceGuid))
            return false;

        if (!reader.TryRead(out value.OwnerGuid))
            return false;

        if (!reader.TryRead(out value.RequesterGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}