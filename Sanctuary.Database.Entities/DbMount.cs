using System;

namespace Sanctuary.Database.Entities;

public class DbMount
{
    public int Id { get; set; }

    public bool IsUpgraded { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ulong CharacterGuid { get; set; }
    public DbCharacter Character { get; set; } = null!;
}