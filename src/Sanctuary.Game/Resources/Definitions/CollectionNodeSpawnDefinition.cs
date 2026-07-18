using System;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class CollectionNodeSpawnDefinition
{
    public int Id { get; set; }
    public required string Pool { get; set; }
    public float[] Position { get; set; } = new float[3];
    public float Heading { get; set; }

    [JsonIgnore]
    public Vector4 SpawnPosition => new(Position[0], Position[1], Position[2], 1f);

    [JsonIgnore]
    public Quaternion SpawnRotation => new(MathF.Sin(Heading), 0f, MathF.Cos(Heading), 0f);
}
