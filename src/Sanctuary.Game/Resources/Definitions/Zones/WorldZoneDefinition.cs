using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions.Zones;

public sealed class WorldZoneDefinition : BaseZoneDefinition
{
    public List<ZoneAreaDefinition> AreaDefinitions { get; set; } = [];

    public List<int> EnabledClientAreaIds { get; set; } = [];

    public List<SeasonalClientAreaDefinition> SeasonalClientAreas { get; set; } = [];
}