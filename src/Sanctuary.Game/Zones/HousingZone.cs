using System;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.Zones;

public sealed class HousingZone : BaseZone
{
    public HousingZone(HousingZoneDefinition zoneDefinition, IServiceProvider serviceProvider)
        : base(zoneDefinition, serviceProvider)
    {
    }

    public override void OnClientIsReady(Player player)
    {
        base.OnClientIsReady(player);

        SendShopData(player);
    }
}
