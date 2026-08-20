using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Housing;

public sealed class HouseManager : IHouseManager
{
    private readonly IResourceManager _resourceManager;
    private readonly IZoneManager _zoneManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly ILogger<HouseManager> _logger;

    public HouseManager(
        IResourceManager resourceManager,
        IZoneManager zoneManager,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        ILogger<HouseManager> logger)
    {
        _resourceManager = resourceManager;
        _zoneManager = zoneManager;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public IReadOnlyList<DbHouse> GetOwnedHouses(ulong characterId)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Houses
            .AsNoTracking()
            .Where(house => house.CharacterId == characterId)
            .OrderBy(house => house.Id)
            .ToList()
            .Where(house => IsAvailableHouse(house.ZoneDefinitionId))
            .ToList();
    }

    public EnterHouseResult EnterOwnedHouse(Player player, int zoneDefinitionId)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var house = dbContext.Houses
            .AsNoTracking()
            .SingleOrDefault(entry =>
                entry.CharacterId == characterId &&
                entry.ZoneDefinitionId == zoneDefinitionId);

        return EnterHouse(player, house);
    }

    public EnterHouseResult EnterHouse(Player player, ulong houseGuid)
    {
        ulong houseId;

        try
        {
            houseId = GuidHelper.GetHouseId(houseGuid);
        }
        catch (ArgumentException)
        {
            return EnterHouseResult.HouseNotFound;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var house = dbContext.Houses
            .AsNoTracking()
            .SingleOrDefault(entry => entry.Id == houseId);

        return EnterHouse(player, house);
    }

    public EnterHouseResult VisitHouse(Player player, int zoneDefinitionId, string ownerName)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var ownerId = dbContext.Characters
            .Where(character => character.FullName == ownerName)
            .Select(character => (ulong?)character.Id)
            .FirstOrDefault();

        if (ownerId is null)
            return EnterHouseResult.HouseNotFound;

        var house = dbContext.Houses
            .AsNoTracking()
            .SingleOrDefault(entry =>
                entry.CharacterId == ownerId.Value &&
                entry.ZoneDefinitionId == zoneDefinitionId);

        return EnterHouse(player, house);
    }

    public bool LeaveHouse(Player player)
    {
        if (player.Zone is not HousingZone)
            return false;

        var position = player.StartingZonePosition == default
            ? _zoneManager.StartingZone.SpawnPosition
            : player.StartingZonePosition;
        var rotation = player.StartingZoneRotation == default
            ? _zoneManager.StartingZone.SpawnRotation
            : player.StartingZoneRotation;

        return player.TeleportToZone(
            _zoneManager.StartingZone,
            position,
            rotation);
    }

    private EnterHouseResult EnterHouse(Player player, DbHouse? house)
    {
        if (house is null || !IsAvailableHouse(house.ZoneDefinitionId))
            return EnterHouseResult.HouseNotFound;

        if (player.Zone is not WorldZone and not HousingZone)
            return EnterHouseResult.UnsupportedSourceZone;

        var playerId = GuidHelper.GetPlayerId(player.Guid);

        var isOwner = playerId == house.CharacterId;
        var isFriend = player.Friends.Any(friend => friend.Guid == GuidHelper.GetPlayerGuid(house.CharacterId));

        if (!isOwner &&
            ((house.IsMembersOnly && player.MembershipStatus == 0) ||
                (!isFriend && (!house.IsPublished || house.IsLocked))))
        {
            return EnterHouseResult.NotAuthorized;
        }

        if (!_zoneManager.TryGetOrCreateZoneInstance(
                house.ZoneDefinitionId,
                house.CharacterId,
                out var zone))
        {
            return EnterHouseResult.ZoneUnavailable;
        }

        if (!player.TeleportToZone(zone, zone.SpawnPosition, zone.SpawnRotation))
            return EnterHouseResult.TransferFailed;

        UpdateLastVisited(house.Id);
        return EnterHouseResult.Success;
    }

    private void UpdateLastVisited(ulong houseId)
    {
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            dbContext.Houses
                .Where(house => house.Id == houseId)
                .ExecuteUpdate(setters => setters.SetProperty(house => house.LastVisited, DateTimeOffset.UtcNow));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to update the last visit for house {HouseId}.", houseId);
        }
    }

    private bool IsAvailableHouse(int zoneDefinitionId)
    {
        return _resourceManager.Zones.TryGetValue(zoneDefinitionId, out var definition) &&
            definition is HousingZoneDefinition;
    }
}
