using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class HousingHelper
{
    private const int HouseDefinitionIdBits = 8;
    private const ulong HouseDefinitionIdMask = (1UL << HouseDefinitionIdBits) - 1;

    public static IEnumerable<HouseDefinition> GetStarterHouses(IResourceManager resourceManager) =>
        resourceManager.Houses.Values.Where(house => house.IsStarter).OrderBy(house => house.Id);

    public static IEnumerable<HouseDefinition> GetOwnedHouses(IResourceManager resourceManager, Player player) =>
        player.ChatCommandRole == ChatCommandRole.Player
            ? GetStarterHouses(resourceManager)
            : resourceManager.Houses.Values.Where(house => CanEnterHouseZone(resourceManager, player, house.ZoneId)).OrderBy(house => house.Id);

    public static bool TryGetHouseDefinition(IResourceManager resourceManager, int zoneDefinitionId, [MaybeNullWhen(false)] out HouseDefinition houseDefinition)
    {
        houseDefinition = resourceManager.Houses.Values.OrderBy(house => house.Id).FirstOrDefault(house => house.ZoneId == zoneDefinitionId);

        return houseDefinition is not null;
    }

    public static ulong GetHouseGuid(ulong ownerGuid, HouseDefinition houseDefinition) =>
        (ownerGuid << HouseDefinitionIdBits) | (uint)houseDefinition.Id;

    public static bool TryGetOwnedHouse(IResourceManager resourceManager, Player player, ulong houseGuid,
        [MaybeNullWhen(false)] out HouseDefinition houseDefinition)
    {
        var houseDefinitionId = (int)(houseGuid & HouseDefinitionIdMask);

        houseDefinition = houseGuid >> HouseDefinitionIdBits == player.Guid
            ? GetOwnedHouses(resourceManager, player).FirstOrDefault(house => house.Id == houseDefinitionId)
            : null;

        return houseDefinition is not null;
    }

    public static bool CanEnterHouseZone(IResourceManager resourceManager, Player player, int zoneDefinitionId) =>
        resourceManager.Zones.TryGetValue(zoneDefinitionId, out var zoneDefinition)
        && zoneDefinition is HousingZoneDefinition { IsBroken: false } housingZoneDefinition
        && (!housingZoneDefinition.StaffOnly || player.ChatCommandRole != ChatCommandRole.Player);

    public static bool TryLeaveHouse(Player player, IZoneManager zoneManager) =>
        player.Zone is HousingZone
        && player.TeleportToZone(zoneManager.StartingZoneDefinitionId, null, player.StartingZonePosition, player.StartingZoneRotation);

    public static PlayerHousingInstanceInfo GetInstanceInfo(ulong ownerGuid, string ownerName, HouseDefinition houseDefinition) => new()
    {
        OwnerGuid = ownerGuid,
        InstanceGuid = GetHouseGuid(ownerGuid, houseDefinition),
        NameId = houseDefinition.NameId,
        OwnerName = ownerName,
        IconId = houseDefinition.Icon,
        LastVisited = DateTimeOffset.UtcNow,
        WhenCreated = DateTimeOffset.UtcNow
    };

    public static HousingPacketInstanceList GetInstanceList(IResourceManager resourceManager, Player player) => new()
    {
        PlayerGuid = player.Guid,
        Instances = GetOwnedHouses(resourceManager, player).Select(house => GetInstanceInfo(player.Guid, player.Name.FullName, house)).ToList()
    };
}
