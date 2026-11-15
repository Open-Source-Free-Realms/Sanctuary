using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class PlayerHousingInstanceData
{
    public ulong HouseGuid;
    public ulong OwnerGuid;

    public string? OwnerName;

    public long OwnerLaunchTicket;

    public int Unknown7;

    public int NameId;
    public string? Name;

    public bool IsLocked;
    public bool IsFloraAllowed;

    public int MaxFixtureCount;
    public int MaxLandmarkCount;

    public int Unknown15;

    public bool Unknown14;

    public bool Preview;

    public int CurFixtureCount;
    public int CurLandmarkCount;

    public int IconId;

    public bool Unknown18;

    public int FurnitureScore;

    public long LastVisited;

    public bool IsMembersOnly;

    public string? Unknown23;
    public string? Unknown22;

    public FactoryInstanceData? FactoryInstance;

    public bool Unknown24;

    public Dictionary<uint, FixtureInstance> Fixtures = new();

    public Dictionary<int, InstancePermission> Permissions = new();

    public List<BoundingBox> BuildAreas = new();

    public void Serialize(PacketWriter writer)
    {
        writer.Write(HouseGuid);
        writer.Write(OwnerGuid);

        writer.Write(OwnerName);

        writer.Write(OwnerLaunchTicket);

        writer.Write(NameId);
        writer.Write(Name);

        writer.Write(Unknown7);

        writer.Write(MaxFixtureCount);
        writer.Write(MaxLandmarkCount);

        writer.Write(Fixtures);

        writer.Write(Permissions);

        writer.Write(IsLocked);
        writer.Write(IsFloraAllowed);

        writer.Write(CurFixtureCount);
        writer.Write(CurLandmarkCount);

        writer.Write(Unknown14);

        writer.Write(Unknown15);

        writer.Write(BuildAreas);

        writer.Write(IconId);

        writer.Write(Preview);

        writer.Write(Unknown18);

        writer.Write(FurnitureScore);

        writer.Write(LastVisited);

        writer.Write(IsMembersOnly);

        writer.Write(Unknown22);
        writer.Write(Unknown23);

        writer.Write(Unknown24);

        writer.Write(FactoryInstance is not null);

        FactoryInstance?.Serialize(writer);
    }
}