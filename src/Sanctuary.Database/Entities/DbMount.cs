using System;

namespace Sanctuary.Database.Entities;

public class DbMount
{
    public int Id { get; set; }

    public int Tint { get; set; }
    public int Definition { get; set; }
    public bool IsUpgraded { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}