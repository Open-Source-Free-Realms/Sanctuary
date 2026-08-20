using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public class ZoneManager : IZoneManager
{
    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private static int _uniqueId = 1;

    private readonly ConcurrentDictionary<(int, ulong?), IZone> _zones = new();

    private const int StartingZoneDefinitionId = 1;
    public WorldZone StartingZone { get; private set; } = null!;

    public ZoneManager(
        ILoggerFactory loggerFactory,
        IResourceManager resourceManager,
        IServiceProvider serviceProvider,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = loggerFactory.CreateLogger<ZoneManager>();

        _resourceManager = resourceManager;
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;
    }

    public bool Load()
    {
        if (!TryCreateStartingZone(StartingZoneDefinitionId, out var startingZone))
            return false;

        StartingZone = startingZone;

        return true;
    }

    public bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player)
    {
        player = default;

        foreach (var zone in _zones)
        {
            if (zone.Value.TryGetPlayer(guid, out player))
                return true;
        }

        return false;
    }

    public bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player)
    {
        player = default;

        foreach (var zone in _zones)
        {
            foreach (var zonePlayer in zone.Value.Players)
            {
                if (zonePlayer.Name.FullName == name)
                {
                    player = zonePlayer;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryCreateStartingZone(int definitionId, [MaybeNullWhen(false)] out WorldZone zone)
    {
        zone = default;

        if (!_resourceManager.Zones.TryGetValue(definitionId, out var zoneDefinition))
            return false;

        if (zoneDefinition is not WorldZoneDefinition worldZoneDefinition)
            return false;

        zone = new WorldZone(worldZoneDefinition, _serviceProvider)
        {
            Id = _uniqueId++
        };

        zone.OnStart();

        return _zones.TryAdd((zone.DefinitionId, null), zone);
    }

    private bool TryGetOrCreateZoneInstance(int zoneDefinitionId, ulong ownerId, [MaybeNullWhen(false)] out IZone zone)
    {
        if (_zones.TryGetValue((zoneDefinitionId, ownerId), out zone))
            return true;

        if (!_resourceManager.Zones.TryGetValue(zoneDefinitionId, out var zoneDefinition))
        {
            zone = null;
            return false;
        }

        BaseZone? newZone = zoneDefinition switch
        {
            HousingZoneDefinition housingZoneDefinition =>
                TryCreateHousingZone(housingZoneDefinition, ownerId, out var housingZone) ? housingZone : null,
            CombatZoneDefinition combatZoneDefinition => new CombatZone(combatZoneDefinition, _serviceProvider)
            {
                Id = _uniqueId++,
                OwnerId = ownerId
            },
            _ => null
        };

        if (newZone is null)
        {
            zone = null;
            return false;
        }

        // Two callers can race to create the first instance for the same (zoneDefinitionId, ownerId)
        // pair - e.g. two party members both zoning into a not-yet-created dungeon at once. Whichever
        // TryAdd loses just discards its own zone (never started, so this is cheap) and hands back
        // whichever instance actually won.
        if (!_zones.TryAdd((zoneDefinitionId, ownerId), newZone))
        {
            newZone.Dispose();
            return _zones.TryGetValue((zoneDefinitionId, ownerId), out zone);
        }

        newZone.OnStart();
        zone = newZone;
        return true;
    }

    private bool TryCreateHousingZone(HousingZoneDefinition housingZoneDefinition, ulong ownerId,
        [MaybeNullWhen(false)] out HousingZone zone)
    {
        zone = null;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouse = dbContext.Houses.SingleOrDefault(house =>
            house.CharacterId == ownerId && house.ZoneDefinitionId == housingZoneDefinition.Id);

        if (dbHouse is null)
        {
            dbHouse = new DbHouse
            {
                CharacterId = ownerId,
                ZoneDefinitionId = housingZoneDefinition.Id
            };

            dbContext.Houses.Add(dbHouse);

            try
            {
                if (dbContext.SaveChanges() <= 0)
                {
                    _logger.LogWarning("Failed to create house for character {characterId}.", ownerId);
                    return false;
                }
            }
            catch (DbUpdateException)
            {
                // Another request already created this house type for this character
                // ((CharacterId, ZoneDefinitionId) is unique) between our SingleOrDefault check
                // and this SaveChanges call. That's not a failure - just use the row that won.
                dbContext.Entry(dbHouse).State = EntityState.Detached;
                dbHouse = dbContext.Houses.SingleOrDefault(house =>
                    house.CharacterId == ownerId && house.ZoneDefinitionId == housingZoneDefinition.Id);

                if (dbHouse is null)
                {
                    _logger.LogWarning("Failed to create or recover house for character {characterId}.", ownerId);
                    return false;
                }
            }
        }

        zone = new HousingZone(housingZoneDefinition, _serviceProvider)
        {
            Id = _uniqueId++,
            OwnerId = ownerId
        };

        return true;
    }
}