using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class CandidateRatingInfo : ISerializableType
{
    public string? InstanceGuid;
    public string? Description;
    public string? KeywordList;

    public float Rating;
    public int Votes;

    public bool HasRating;
    public bool CanVote;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(InstanceGuid);
        writer.Write(Description);
        writer.Write(KeywordList);

        writer.Write(Rating);
        writer.Write(Votes);

        writer.Write(HasRating);
        writer.Write(CanVote);
    }
}