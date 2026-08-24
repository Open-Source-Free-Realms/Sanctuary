using System;

namespace Sanctuary.Database.Entities;

public class DbHouseVote
{
    public ulong HouseId { get; set; }
    public DbHouse House { get; set; } = null!;

    public ulong CharacterId { get; set; }
    public int Value { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
}
