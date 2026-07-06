using System;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Sanctuary.Game.Resources.Definitions;

public class NpcDefinition
{
    public int Id { get; set; }

    public int NameId { get; set; }
    public string? Name { get; set; }

    public int ModelId { get; set; }
    public string ModelFileName { get; set; } = null!;

    public string? TextureAlias { get; set; }

    public bool Static { get; set; } = true;

    public float[] SpawnPosition { get; set; } = new float[3];
    public float SpawnHeading { get; set; }

    [JsonIgnore]
    public Vector4 Position => new(SpawnPosition[0], SpawnPosition[1], SpawnPosition[2], 1f);

    [JsonIgnore]
    public Quaternion Rotation => new(MathF.Sin(SpawnHeading), 0f, MathF.Cos(SpawnHeading), 0f);
}
