using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions.Zones;

public sealed class StartingZoneDefinition : BaseZoneDefinition
{
    public List<ZoneAreaDefinition> AreaDefinitions { get; set; } = [];
}