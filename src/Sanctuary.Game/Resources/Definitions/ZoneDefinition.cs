using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public class ZoneDefinition
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool StartingZone { get; set; }

    public List<ZoneAreaDefinition> AreaDefinitions { get; set; } = new();

    public GzneDefinition Gzne { get; set; } = null!;
}