using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

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
    private static int _uniqueId = 1;

    private readonly ConcurrentDictionary<(int, ulong?), IZone> _zones = new();

    public int StartingZoneDefinitionId => 1;

    public WorldZone StartingZone { get; private set; } = null!;

    public IEnumerable<IZone> Zones => _zones.Values;

    public ZoneManager(
        ILoggerFactory loggerFactory,
        IResourceManager resourceManager,
        IServiceProvider serviceProvider)
    {
        _logger = loggerFactory.CreateLogger<ZoneManager>();

        _resourceManager = resourceManager;
        _serviceProvider = serviceProvider;
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

        zone.Start();

        return _zones.TryAdd((zone.DefinitionId, null), zone);
    }

    public bool TryGetOrCreateZoneInstance(int zoneDefinitionId, ulong? ownerId, [MaybeNullWhen(false)] out IZone zone)
    {
        var key = (zoneDefinitionId, ownerId);

        var storedZone = _zones.GetOrAdd(key, _ => CreateZoneInstance(zoneDefinitionId, ownerId)!);

        if (storedZone is null)
        {
            _zones.TryRemove(key, out _);
            zone = null;
            return false;
        }

        storedZone.Start();

        zone = storedZone;
        return true;
    }

    private IZone? CreateZoneInstance(int zoneDefinitionId, ulong? ownerId)
    {
        if (!_resourceManager.Zones.TryGetValue(zoneDefinitionId, out var zoneDefinition))
        {
            return null;
        }

        return zoneDefinition switch
        {
            WorldZoneDefinition worldZoneDefinition => new WorldZone(worldZoneDefinition, _serviceProvider)
            {
                Id = _uniqueId++,
                OwnerId = ownerId
            },
            HousingZoneDefinition housingZoneDefinition when ownerId is not null =>
                TryGetHousingZone(housingZoneDefinition, ownerId.Value, out var housingZone) ? housingZone : null,
            CombatZoneDefinition combatZoneDefinition when ownerId is not null => new CombatZone(combatZoneDefinition, _serviceProvider)
            {
                Id = _uniqueId++,
                OwnerId = ownerId
            },
            _ => null
        };
    }

    public void RemoveZoneInstance(IZone zone)
    {
        var key = (zone.DefinitionId, zone.OwnerId);

        ((ICollection<KeyValuePair<(int, ulong?), IZone>>)_zones).Remove(new(key, zone));
    }

    private bool TryGetHousingZone(HousingZoneDefinition housingZoneDefinition, ulong ownerId,
        [MaybeNullWhen(false)] out HousingZone zone)
    {
        // NOTE: As of PR-116, this MIGHT NOT be hooked up properly. It will be the responsibility
        // of anyone working on housing to properly match conventions/make adjustments. Once that is 
        // done, remove this note.
        //
        // The commented code will be left as a convenience. You will need to give database access
        // to the class for it to work.

        // zone = null;

        // using var dbContext = _dbContextFactory.CreateDbContext();

        // var dbHouse = dbContext.Houses.SingleOrDefault(house =>
        //     house.CharacterId == ownerId && house.ZoneDefinitionId == housingZoneDefinition.Id);

        // if (dbHouse is null)
        //     return false;

        zone = new HousingZone(housingZoneDefinition, _serviceProvider)
        {
            Id = _uniqueId++,
            OwnerId = ownerId
        };

        return true;
    }
}