using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class RatingDataEntry : ISerializableType
{
    public string? HouseGuid;
    public string? OwnerName;
    public string? HouseName;

    public ulong OwnerGuid;

    public string? Snapshot;
    public string? Description;
    public string? KeywordList;

    public float Rating;
    public float NumVotes;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(HouseGuid);
        writer.Write(OwnerName);
        writer.Write(HouseName);

        writer.Write(OwnerGuid);

        writer.Write(Snapshot);
        writer.Write(Description);
        writer.Write(KeywordList);

        writer.Write(Rating);
        writer.Write(NumVotes);
    }
}