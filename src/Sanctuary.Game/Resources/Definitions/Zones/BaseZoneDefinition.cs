using System.Numerics;
using System.Text.Json.Serialization;

using Sanctuary.Core.IO;

namespace Sanctuary.Game.Resources.Definitions.Zones;

[JsonDerivedType(typeof(WorldZoneDefinition), "World")]
[JsonDerivedType(typeof(CombatZoneDefinition), "Dungeon")]
[JsonDerivedType(typeof(HousingZoneDefinition), "Housing")]
public abstract class BaseZoneDefinition
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public float TileSize { get; set; }

    /// <summary>
    /// X axies
    /// </summary>
    public int StartLongitude { get; set; }
    public int EndLongitude { get; set; }

    /// <summary>
    /// Z axies
    /// </summary>
    public int StartLatitude { get; set; }
    public int EndLatitude { get; set; }

    public string? Sky { get; set; }

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 SpawnPosition { get; set; }

    [JsonConverter(typeof(QuaternionJsonConverter))]
    public Quaternion SpawnRotation { get; set; }

    public string[]? Scripts { get; set; }
}