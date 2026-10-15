using System;

using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.Zones;

public sealed class CombatZone : BaseZone
{
    public CombatZone(CombatZoneDefinition zoneDefinition, IServiceProvider serviceProvider)
        : base(zoneDefinition, serviceProvider)
    {
    }
}