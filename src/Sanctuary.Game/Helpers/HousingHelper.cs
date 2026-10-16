using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
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

    private const int MaxFixtureCount = 9999;
    private const int MaxLandmarkCount = 100;

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

    public static ulong GetHouseOwnerGuid(ulong houseGuid) => houseGuid >> HouseDefinitionIdBits;

    public static bool TryEnterHouse(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager,
        Player player, ulong houseGuid, out HousingError housingError)
    {
        var ownerGuid = GetHouseOwnerGuid(houseGuid);
        var houseDefinitionId = (int)(houseGuid & HouseDefinitionIdMask);

        var owner = ownerGuid == player.Guid ? player : zoneManager.TryGetPlayer(ownerGuid, out var onlineOwner) ? onlineOwner : null;
        var houseDefinition = (owner is null ? GetStarterHouses(resourceManager) : GetOwnedHouses(resourceManager, owner)).FirstOrDefault(house => house.Id == houseDefinitionId);

        if (houseDefinition is null || !CanEnterHouseZone(resourceManager, player, houseDefinition.ZoneId)
            || !resourceManager.Zones.TryGetValue(houseDefinition.ZoneId, out var zoneDefinition))
        {
            housingError = HousingError.ZoneNotFound;
            return false;
        }

        if (ownerGuid != player.Guid && !player.HouseInvites.Contains(houseGuid) && IsHouseLocked(dbContextFactory, ownerGuid, houseDefinition.ZoneId))
        {
            housingError = HousingError.PermissionDenied;
            return false;
        }

        player.HouseInvites.Remove(houseGuid);

        if (!player.TeleportToZone(zoneDefinition.Id, ownerGuid, zoneDefinition.SpawnPosition, zoneDefinition.SpawnRotation))
        {
            housingError = HousingError.ZoneFailedToLaunch;
            return false;
        }

        housingError = default;
        return true;
    }

    public static bool IsHouseLocked(IDbContextFactory<DatabaseContext> dbContextFactory, ulong ownerGuid, int zoneDefinitionId)
    {
        var characterId = GuidHelper.GetPlayerId(ownerGuid);

        using var dbContext = dbContextFactory.CreateDbContext();

        return dbContext.Houses.AsNoTracking().Any(house => house.Id == zoneDefinitionId && house.CharacterId == characterId && house.IsLocked);
    }

    public static bool TryLeaveHouse(Player player, IZoneManager zoneManager) =>
        player.Zone is HousingZone
        && player.TeleportToZone(zoneManager.StartingZoneDefinitionId, null, player.StartingZonePosition, player.StartingZoneRotation);

    public static PlayerHousingInstanceData GetInstanceData(ulong ownerGuid, string ownerName, HouseDefinition houseDefinition) => new()
    {
        HouseGuid = GetHouseGuid(ownerGuid, houseDefinition),
        OwnerGuid = ownerGuid,
        OwnerName = ownerName,
        NameId = houseDefinition.NameId,
        IconId = houseDefinition.Icon,
        MaxFixtureCount = MaxFixtureCount,
        MaxLandmarkCount = MaxLandmarkCount,
        IsFloraAllowed = true,
        BuildAreas = houseDefinition.BuildAreas
    };

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

    public static HousingPacketInstanceList GetInstanceList(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, Player player)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = dbContextFactory.CreateDbContext();

        var dbHouses = dbContext.Houses.AsNoTracking().Where(house => house.CharacterId == characterId).ToDictionary(house => house.Id);

        return new HousingPacketInstanceList
        {
            PlayerGuid = player.Guid,
            Instances = GetOwnedHouses(resourceManager, player).Select(house =>
            {
                var instanceInfo = GetInstanceInfo(player.Guid, player.Name.FullName, house);

                if (dbHouses.TryGetValue(house.ZoneId, out var dbHouse))
                {
                    instanceInfo.HouseName = dbHouse.Name;
                    instanceInfo.IsLocked = dbHouse.IsLocked;
                    instanceInfo.IsFloraAllowed = dbHouse.IsFloraAllowed;
                }

                return instanceInfo;
            }).ToList()
        };
    }
}
