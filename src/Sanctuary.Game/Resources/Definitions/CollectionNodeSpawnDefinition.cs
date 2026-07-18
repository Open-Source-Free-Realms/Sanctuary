using System;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class CollectionNodeSpawnDefinition
{
    public int Id { get; set; }
    public float[] Position { get; set; } = new float[3];
    public float Heading { get; set; }

    [JsonIgnore]
    public string Pool { get; set; } = string.Empty;

    [JsonIgnore]
    public int ZoneDefinitionId { get; internal set; }

    [JsonIgnore]
    public Vector4 SpawnPosition => new(Position[0], Position[1], Position[2], 1f);

    [JsonIgnore]
    // NPC heading is encoded in the X/Z components used by the zone spawn packets.
    public Quaternion SpawnRotation => new(MathF.Sin(Heading), 0f, MathF.Cos(Heading), 0f);
}
