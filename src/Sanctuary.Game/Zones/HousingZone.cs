using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.Zones;

public sealed class HousingZone : BaseZone
{
    private const int HousingZoneHeadSize = 10;

    public HousingZone(HousingZoneDefinition zoneDefinition, IServiceProvider serviceProvider)
        : base(zoneDefinition, serviceProvider)
    {
    }

    public override void OnClientIsReady(Player player)
    {
        base.OnClientIsReady(player);

        SendShopData(player);
        SendHousingZoneData(player);
    }

    private void SendHousingZoneData(Player player)
    {
        if (!HousingHelper.TryGetHouseDefinition(_resourceManager, DefinitionId, out var houseDefinition) || OwnerId is not { } ownerGuid)
            return;

        var owner = player.Guid == ownerGuid ? player : Players.FirstOrDefault(zonePlayer => zonePlayer.Guid == ownerGuid);

        player.SendTunneled(new HousingPacketZoneData
        {
            HeadSize = HousingZoneHeadSize,
            InstanceInfo = HousingHelper.GetInstanceInfo(ownerGuid, owner?.Name.FullName ?? string.Empty, houseDefinition)
        });
    }
}
