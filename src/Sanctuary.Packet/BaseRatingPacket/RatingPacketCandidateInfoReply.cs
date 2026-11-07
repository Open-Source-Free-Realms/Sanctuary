using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class RatingPacketCandidateInfoReply : BaseRatingPacket, ISerializablePacket
{
    public new const byte OpCode = 17;

    public ulong Guid;

    public List<CandidateRatingInfo> Candidates = [];

    public RatingPacketCandidateInfoReply() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(Candidates);

        return writer.Buffer;
    }
}