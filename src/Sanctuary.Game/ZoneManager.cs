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

    private readonly object _playerTransitionLock = new();

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

    public bool TryMovePlayerToZone(int zoneDefinitionId, ulong? ownerId, Player player, out IZone zone)
    {
        lock (_playerTransitionLock)
        {
            if (!TryGetOrCreateZoneInstance(zoneDefinitionId, ownerId, out var newZone))
            {
                zone = player.Zone;
                return false;
            }

            zone = newZone;

            // NOTE: this MIGHT be delecate...
            // These SHOULD both always return 'true', but if we want to be extra safe,
            // we can return the original zone the player was in if they fail...
            player.Zone.TryRemovePlayer(player.Guid);
            return newZone.TryAddPlayer(player);
        }
    }


    private bool TryGetOrCreateZoneInstance(int zoneDefinitionId, ulong? ownerId, [MaybeNullWhen(false)] out IZone zone)
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

        // NOTE: This might be called twice from two separate threads (one after the other)
        // This means the zone may be disposed twice. Right now, this seems to be okay, so
        // no protection will be added within 'dispose'.
        lock (_playerTransitionLock)
        {

            if (zone.IsEmpty)
                zone.Dispose();
        }
    }


    private bool PlayerCanAccessZone(BaseZoneDefinition definition, ulong? ownerId) => definition switch
    {
        // NOTE: One day, we may need to worry about code that tries to allow a player into a zone
        // they're not aloud to be inside of (e.g., a private(?) house. Not sure if that existed).
        // The assumption is that would be taken care of elsewhere (i.e., a player wouldn't even
        // be able to see an option to get to a zone they shouldn't be able to get into).
        //
        // This is why we don't pass a player instance here directly.
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

        throw new NotImplementedException();
    }
}