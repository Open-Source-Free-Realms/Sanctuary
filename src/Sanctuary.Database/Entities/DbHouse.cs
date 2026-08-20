using System;

namespace Sanctuary.Database.Entities;

public class DbHouse
{
    public ulong Id { get; set; }

    public int ZoneDefinitionId { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}
