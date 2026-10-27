using System;
using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public class DbItem
{
    public int Id { get; set; }

    public int Tint { get; set; }
    public int Count { get; set; }
    public int Definition { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DbProfile> Profiles { get; set; } = new HashSet<DbProfile>();

    public ICollection<DbPet> Pets { get; set; } = new HashSet<DbPet>();

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}