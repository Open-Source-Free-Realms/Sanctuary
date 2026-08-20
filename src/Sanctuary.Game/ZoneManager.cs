using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
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

    private readonly ConcurrentDictionary<(int, ulong?), Lazy<IZone?>> _zones = new();

    private const int StartingZoneDefinitionId = 1;
    public WorldZone StartingZone { get; private set; } = null!;

    public IEnumerable<IZone> Zones
    {
        get
        {
            foreach (var zoneEntry in _zones.Values)
            {
                if (zoneEntry.IsValueCreated && zoneEntry.Value is { IsDisposed: false } zone)
                    yield return zone;
            }
        }
    }

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

        foreach (var zone in Zones)
        {
            if (zone.TryGetPlayer(guid, out player))
                return true;
        }

        return false;
    }

    public bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player)
    {
        player = default;

        foreach (var zone in Zones)
        {
            foreach (var zonePlayer in zone.Players)
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
        if (TryGetOrCreateZoneInstance(definitionId, null, out var createdZone) && createdZone is WorldZone worldZone)
        {
            zone = worldZone;
            return true;
        }

        zone = null;
        return false;
    }

    public bool TryGetOrCreateZoneInstance(int zoneDefinitionId, ulong? ownerId, [MaybeNullWhen(false)] out IZone zone)
    {
        var key = (zoneDefinitionId, ownerId);

        while (true)
        {
            var zoneEntry = _zones.GetOrAdd(key, _ => new Lazy<IZone?>(
                () => CreateZoneInstance(zoneDefinitionId, ownerId),
                LazyThreadSafetyMode.ExecutionAndPublication));
            IZone? activeZone;

            try
            {
                activeZone = zoneEntry.Value;
            }
            catch (Exception exception)
            {
                TryRemoveZoneEntry(key, zoneEntry);
                _logger.LogError(exception, "Failed to create zone definition {DefinitionId} for owner {OwnerId}.", zoneDefinitionId, ownerId);
                zone = null;
                return false;
            }

            if (activeZone is null)
            {
                TryRemoveZoneEntry(key, zoneEntry);
                zone = null;
                return false;
            }

            if (!activeZone.IsDisposed)
            {
                zone = activeZone;
                return true;
            }

            TryRemoveZoneEntry(key, zoneEntry);
        }
    }

    private IZone? CreateZoneInstance(int zoneDefinitionId, ulong? ownerId)
    {
        BaseZone? zone = null;

        try
        {
            if (!_resourceManager.Zones.TryGetValue(zoneDefinitionId, out var zoneDefinition))
                return null;

            zone = zoneDefinition switch
            {
                WorldZoneDefinition worldZoneDefinition => new WorldZone(worldZoneDefinition, _serviceProvider)
                {
                    Id = NextRuntimeId(),
                    OwnerId = ownerId
                },
                HousingZoneDefinition housingZoneDefinition when ownerId is not null =>
                    TryGetHousingZone(housingZoneDefinition, ownerId.Value, out var housingZone) ? housingZone : null,
                CombatZoneDefinition combatZoneDefinition when ownerId is not null =>
                    new CombatZone(combatZoneDefinition, _serviceProvider)
                    {
                        Id = NextRuntimeId(),
                        OwnerId = ownerId
                    },
                _ => null
            };

            if (zone is null)
                return null;

            zone.OnStart();
            zone.CompleteInitialization();

            return zone.IsDisposed ? null : zone;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to initialize zone definition {DefinitionId} for owner {OwnerId}.", zoneDefinitionId, ownerId);
            zone?.Dispose();
            return null;
        }
    }

    private bool TryRemoveZoneEntry((int, ulong?) key, Lazy<IZone?> zoneEntry)
    {
        return ((ICollection<KeyValuePair<(int, ulong?), Lazy<IZone?>>>)_zones)
            .Remove(new(key, zoneEntry));
    }

    private static int NextRuntimeId()
    {
        return Interlocked.Increment(ref _uniqueId) - 1;
    }

    public void RemoveZoneInstance(IZone zone)
    {
        var key = (zone.DefinitionId, zone.OwnerId);

        if (_zones.TryGetValue(key, out var zoneEntry) && zoneEntry.IsValueCreated &&
            ReferenceEquals(zoneEntry.Value, zone))
        {
            TryRemoveZoneEntry(key, zoneEntry);
        }
    }

    private bool TryGetHousingZone(HousingZoneDefinition housingZoneDefinition, ulong ownerId,
        [MaybeNullWhen(false)] out HousingZone zone)
    {
        zone = null;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouse = dbContext.Houses.SingleOrDefault(house =>
            house.CharacterId == ownerId && house.ZoneDefinitionId == housingZoneDefinition.Id);

        if (dbHouse is null)
            return false;

        zone = new HousingZone(housingZoneDefinition, _serviceProvider)
        {
            Id = NextRuntimeId(),
            OwnerId = ownerId
        };

        return true;
    }

}
