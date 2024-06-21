using System;
using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;

namespace Sanctuary.Game;

public class ZoneManager : IZoneManager
{
    private readonly IResourceManager _resourceManager;
    private readonly IServiceProvider _serviceProvider;

    private readonly ConcurrentDictionary<int, IZone> _zones = new();

    public IZone StartingZone { get; private set; } = null!;

    public IZone? Get(int zoneId) => _zones.TryGetValue(zoneId, out var zone) ? zone : null;

    public ZoneManager(IResourceManager resourceManager, IServiceProvider serviceProvider)
    {
        _resourceManager = resourceManager;
        _serviceProvider = serviceProvider;
    }

    public bool LoadZones()
    {
        var startingZoneLoaded = false;

        foreach (var zoneDefinition in _resourceManager.Zones.Values)
        {
            var zone = ActivatorUtilities.CreateInstance<Zone>(_serviceProvider, zoneDefinition);

            _zones.TryAdd(zone.Definition.Id, zone);

            if (!startingZoneLoaded && zoneDefinition.StartingZone)
            {
                StartingZone = zone;
                startingZoneLoaded = true;
            }
        }

        return startingZoneLoaded;
    }
}