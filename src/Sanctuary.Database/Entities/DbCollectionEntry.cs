using System;

namespace Sanctuary.Database.Entities;

public sealed class DbCollectionEntry
{
    public int CollectionId { get; set; }
    public int EntryId { get; set; }

    public DateTimeOffset Collected { get; set; } = DateTimeOffset.UtcNow;

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}
