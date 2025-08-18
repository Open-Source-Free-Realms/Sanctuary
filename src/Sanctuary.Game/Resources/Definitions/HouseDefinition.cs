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

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 SpawnPosition { get; set; }

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 SpawnRotation { get; set; }

    public List<BoundingBox> BuildAreas { get; set; } = new();
}