using System;

namespace Sanctuary.Database.Entities;

public class DbHouse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsLocked { get; set; }
    public bool IsFloraAllowed { get; set; } = true;
    public bool PetAutospawn { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}
