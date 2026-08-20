using System;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Housing;
using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.Zones;

public sealed class HousingZone : BaseZone
{
    public ulong HouseId { get; }
    public HousingZoneDefinition HousingDefinition { get; }
    public HousingZoneRuntime Runtime { get; }

    public HousingZone(
        HousingZoneDefinition zoneDefinition,
        ulong houseId,
        IServiceProvider serviceProvider)
        : base(zoneDefinition, serviceProvider)
    {
        HouseId = houseId;
        HousingDefinition = zoneDefinition;
        Runtime = new HousingZoneRuntime(this, serviceProvider);
    }

    public override void OnStart()
    {
        base.OnStart();
        Runtime.Initialize();
    }

    public override void OnClientIsReady(Player player)
    {
        Runtime.SendInitialData(player);
        base.OnClientIsReady(player);
    }

    public override void OnClientFinishedLoading(Player player)
    {
        base.OnClientFinishedLoading(player);
        Runtime.OnClientFinishedLoading(player);
    }

    protected override void OnPlayerRemoved(Player player)
    {
        Runtime.OnPlayerRemoved(player);
    }

    protected override void OnDisposing()
    {
        Runtime.Dispose();
    }
}
