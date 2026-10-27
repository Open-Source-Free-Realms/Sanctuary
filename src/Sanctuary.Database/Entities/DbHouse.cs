using System;
using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public class DbHouse
{
    public ulong Id { get; set; }

    public int Definition { get; set; }

    public int NameId { get; set; }
    public string? Name { get; set; }

    public bool IsLocked { get; set; }
    public bool IsFloraAllowed { get; set; }
    public bool PetAutospawn { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastVisited { get; set; }

    public ICollection<DbHouseFixture> Fixtures { get; set; } = new HashSet<DbHouseFixture>();

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}