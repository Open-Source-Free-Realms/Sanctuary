using System;
using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public class DbHouse
{
    public ulong Id { get; set; }

    public int ZoneDefinitionId { get; set; }

    public string? Name { get; set; }

    public bool IsLocked { get; set; }
    public bool IsMembersOnly { get; set; }
    public bool IsFloraAllowed { get; set; } = true;
    public bool PetAutospawn { get; set; }

    public int MaxFixtureCount { get; set; } = 2000;
    public int MaxLandmarkCount { get; set; }
    public int FurnitureScore { get; set; }

    public bool IsPublished { get; set; }
    public int Votes { get; set; }
    public float Rating { get; set; }

    public string Description { get; set; } = string.Empty;
    public string KeywordList { get; set; } = string.Empty;
    public string? CustomizationData { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastVisited { get; set; } = DateTimeOffset.UtcNow;

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;

    public ICollection<DbHouseFixture> Fixtures { get; set; } = new HashSet<DbHouseFixture>();
    public ICollection<DbHouseVote> VoteRecords { get; set; } = new HashSet<DbHouseVote>();
}
