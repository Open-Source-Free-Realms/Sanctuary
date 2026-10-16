using System;

namespace Sanctuary.Database.Entities;

public class DbHouseVote
{
    public int HouseId { get; set; }

    public ulong VoterId { get; set; }

    public float Rating { get; set; }
    public string Category { get; set; } = string.Empty;

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ulong CharacterId { get; set; }
    public DbHouse House { get; set; } = null!;
}
