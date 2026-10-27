using System;

namespace Sanctuary.Database.Entities;

public class DbHouseFixture
{
    public ulong Id { get; set; }

    public int Tint { get; set; }
    public int Definition { get; set; }

    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }

    public float RotationX { get; set; }
    public float RotationY { get; set; }
    public float RotationZ { get; set; }
    public float RotationW { get; set; }

    public float Scale { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public ulong HouseId { get; set; }
    public DbHouse House { get; set; } = null!;
}