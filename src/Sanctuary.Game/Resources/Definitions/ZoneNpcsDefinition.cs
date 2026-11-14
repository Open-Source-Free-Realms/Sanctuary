using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

using Sanctuary.Core.IO;

namespace Sanctuary.Game.Resources.Definitions;

public class ZoneNpcsDefinition
{
    public List<NpcDefinition> Definitions { get; set; } = new();
    public List<ZoneNpcSpawnDefinition> Spawns { get; set; } = new();
}

public class ZoneNpcSpawnDefinition
{
    public int NpcId { get; set; }

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 Position { get; set; }

    public float Heading { get; set; }
}
