using System;

namespace Sanctuary.Database.Entities;

public class DbIgnore
{
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ulong IgnoreCharacterId { get; set; }
    public DbCharacter IgnoreCharacter { get; set; } = null!;

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}