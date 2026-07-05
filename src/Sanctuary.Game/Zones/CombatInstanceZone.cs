using System;

using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.Zones;

public sealed class CombatInstanceZone : StartingZone
{
    public CombatInstanceZone(StartingZoneDefinition zoneDefinition, IServiceProvider serviceProvider)
        : base(zoneDefinition, serviceProvider)
    {
    }
}
