using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public class ZoneManager : IZoneManager
{
    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IServiceProvider _serviceProvider;

    private static int _uniqueId = 1;

    private readonly ConcurrentDictionary<int, IZone> _zones = new();

    private const int StartingZoneDefinitionId = 1;
    public StartingZone StartingZone { get; private set; } = null!;
    private CombatInstanceZone? _combatInstanceZone;

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

        var npcJsonPath = Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "Npcs",
            "npcs.json"
        );

        if (!File.Exists(npcJsonPath))
        {
            npcJsonPath = Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "Npcs",
                "npcs.json"
            );
        }

        var imported = NpcJsonLoader.LoadIntoZone(startingZone, npcJsonPath);

        _logger.LogInformation("Loaded {Count} NPC(s).", imported);

        return true;
    }

    public bool TryGetOrCreateCombatInstance([MaybeNullWhen(false)] out CombatInstanceZone zone)
    {
        if (_combatInstanceZone is not null)
        {
            zone = _combatInstanceZone;
            return true;
        }

        zone = default;

        if (!_resourceManager.Zones.TryGetValue(StartingZoneDefinitionId, out var zoneDefinition))
            return false;

        if (zoneDefinition is not StartingZoneDefinition startingZoneDefinition)
            return false;

        var combatInstanceZone = new CombatInstanceZone(startingZoneDefinition, _serviceProvider)
        {
            Id = _uniqueId++
        };

        if (!_zones.TryAdd(combatInstanceZone.Id, combatInstanceZone))
            return false;

        _combatInstanceZone = combatInstanceZone;
        zone = combatInstanceZone;

        _logger.LogInformation(
            "Created singleton combat instance zone. ( ZoneId: {zoneId}, ZoneName: {zoneName} )",
            combatInstanceZone.Id,
            combatInstanceZone.Name);

        return true;
    }

    public bool IsCombatInstance(IZone zone)
    {
        return _combatInstanceZone is not null && ReferenceEquals(_combatInstanceZone, zone);
    }


    public IEnumerable<Player> GetPlayers()
    {
        return _zones.Values.SelectMany(x => x.Players).ToArray();
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

    private bool TryCreateStartingZone(int definitionId, [MaybeNullWhen(false)] out StartingZone zone)
    {
        zone = default;

        if (!_resourceManager.Zones.TryGetValue(definitionId, out var zoneDefinition))
            return false;

        if (zoneDefinition is not StartingZoneDefinition startingZoneDefinition)
            return false;

        zone = new StartingZone(startingZoneDefinition, _serviceProvider)
        {
            Id = _uniqueId++
        };

        return _zones.TryAdd(zone.Id, zone);
    }
}
