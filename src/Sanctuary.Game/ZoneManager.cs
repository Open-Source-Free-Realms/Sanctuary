using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
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

    private int NextID() => Interlocked.Increment(ref _uniqueId) - 1;

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
            Id = NextID()
        };

        if (!_zones.TryAdd((zone.DefinitionId, null), zone))
            return false;

        zone.OnStart();

        return true;
    }

    public bool TryGetOrCreateZoneInstance(int zoneDefinitionId, ulong? ownerId, [MaybeNullWhen(false)] out IZone zone)
    {
        if (!_resourceManager.Zones.TryGetValue(zoneDefinitionId, out var zoneDefinition))
        {
            zone = null;
            return false;
        }

        if (!PlayerCanAccessZone(zoneDefinition, ownerId))
        {
            zone = null;
            return false;
        }

        var key = (zoneDefinitionId, ownerId);

        zone = _zones.GetOrAdd(key, _ => CreateZoneInstance(zoneDefinition, ownerId));
        zone.OnStart();

        return true;
    }

    private IZone CreateZoneInstance(BaseZoneDefinition zoneDefinition, ulong? ownerId) => zoneDefinition switch
    {
        WorldZoneDefinition worldZoneDefinition => new WorldZone(worldZoneDefinition, _serviceProvider)
        {
            Id = NextID(),
            OwnerId = ownerId // Note: should always be 'null'.
        },
        HousingZoneDefinition housingZoneDefinition => new HousingZone(housingZoneDefinition, _serviceProvider)
        {
            Id = NextID(),
            OwnerId = ownerId
        },
        CombatZoneDefinition combatZoneDefinition => new CombatZone(combatZoneDefinition, _serviceProvider)
        {
            Id = NextID(),
            OwnerId = ownerId
        },
        _ => throw new InvalidOperationException($"Unhandled zone definition type: {zoneDefinition.GetType()}")
    };

    public void RemoveZoneInstance(IZone zone)
    {
        var key = (zone.DefinitionId, zone.OwnerId);
        _zones.TryRemove(new(key, zone));
    }

    public void EvictIfEmpty(IZone zone)
    {
        var isStartingZone = zone.DefinitionId == StartingZoneDefinitionId && zone.OwnerId is null;

        if (isStartingZone)
            return;

        // NOTE: So the zone instance itself has access to players and its lock
        // so this will handle the empty check. 
        // Another option is to build helpers to expose something like an 
        // 'isEmpty' which we can call here.
        zone.TryMarkDisposedIfEmpty();
    }

    private bool PlayerCanAccessZone(BaseZoneDefinition definition, ulong? ownerId) => definition switch
    {
        WorldZoneDefinition => true,
        CombatZoneDefinition => ownerId is not null,
        HousingZoneDefinition housing => ownerId is not null && PlayerOwnsHouse(ownerId.Value, housing.Id),
        _ => false
    };

    private bool PlayerOwnsHouse(ulong ownerId, int zoneDefinitionId)
    {
        // NOTE: I'm not sure I like this function living here...
        // maybe just throw this DB logic into the 'PlayerCanAccessZone' funciton in
        // the first place..? Idk

        // TODO: fetch from DB

        // using var dbContext = _dbContextFactory.CreateDbContext();

        // var dbHouse = dbContext.Houses.SingleOrDefault(house =>
        //     house.CharacterId == ownerId && house.ZoneDefinitionId == zoneDefinitionId);

        // if (dbHouse is null)
        //     return false;

        return true;
    }
}