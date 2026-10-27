using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources.Definitions;

public class HouseDefinition
{
    public int Id { get; set; }

    public int NameId { get; set; }
    public int ZoneId { get; set; }

    public int Icon { get; set; }

    /// <summary>
    /// House items that grant ownership of this house.
    /// </summary>
    public List<int> ItemDefinitionIds { get; set; } = new();

    /// <summary>
    /// Zone object (usually the lot gate or front door) players click to leave the house.
    /// When 0, an exit door is spawned at <see cref="SpawnPosition"/> instead.
    /// </summary>
    public int ExitObjectId { get; set; }

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 ExitPosition { get; set; }

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 SpawnPosition { get; set; }

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 SpawnRotation { get; set; }

    public List<BoundingBox> BuildAreas { get; set; } = new();
}