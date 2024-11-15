using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class PlayerHousingInstanceInfo : ISerializableType
{
    public ulong OwnerGuid;
    public ulong InstanceGuid;

    public int NameId;

    public string? OwnerName;
    public string? HouseName;

    public int IconId;
    public int FixtureCount;
    public int FurnitureScore;

    public DateTime LastVisited;

    public bool IsLocked;
    public bool IsMembersOnly;
    public bool IsFloraAllowed;

    public string? Description;
    public string? KeywordList;
    public string? Unknown21;

    public float Rating;
    public int Votes;

    public bool HasRating;
    public bool CanVote;

    public int FactoryPlotId;

    public long WhenCreated;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(OwnerGuid);
        writer.Write(InstanceGuid);

        writer.Write(NameId);

        writer.Write(HouseName);
        writer.Write(OwnerName);

        writer.Write(IconId);
        writer.Write(FixtureCount);
        writer.Write(FurnitureScore);

        writer.Write(LastVisited);

        writer.Write(IsLocked);
        writer.Write(IsMembersOnly);
        writer.Write(IsFloraAllowed);

        writer.Write(Description);
        writer.Write(KeywordList);
        writer.Write(Unknown21);

        writer.Write(Rating);
        writer.Write(Votes);

        writer.Write(HasRating);
        writer.Write(CanVote);

        writer.Write(FactoryPlotId);

        writer.Write(WhenCreated);
    }
}