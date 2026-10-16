using System;
using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public class DbHouse
{
    public int Id { get; set; }

    public int ZoneId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsLocked { get; set; }
    public bool IsFloraAllowed { get; set; } = true;
    public bool PetAutospawn { get; set; }

    public bool IsPublished { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;

    public ICollection<DbHouseVote> Votes { get; set; } = new HashSet<DbHouseVote>();
}
