using System;

namespace Sanctuary.Database.Entities;

public class DbHouseFixture
{
    public int Id { get; set; }

    public ulong HouseId { get; set; }
    public DbHouse House { get; set; } = null!;

    public Guid PlacementToken { get; set; } = Guid.NewGuid();

    public int ItemDefinitionId { get; set; }
    public int TintId { get; set; }

    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float PositionW { get; set; }

    public float RotationX { get; set; }
    public float RotationY { get; set; }
    public float RotationZ { get; set; }
    public float RotationW { get; set; }

    public float Scale { get; set; } = 1f;
    public string? CustomizationData { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
}
