using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public class WorldEventManager : IWorldEventManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly Lazy<ConcurrentDictionary<string, bool>> _events;

    public WorldEventManager(IServiceProvider serviceProvider, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;

        _events = new(LoadEvents);
    }

    private ConcurrentDictionary<string, bool> LoadEvents()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return new(dbContext.WorldEvents
            .AsNoTracking()
            .ToDictionary(x => x.Key, x => x.IsActive), StringComparer.OrdinalIgnoreCase);
    }

    public bool IsActive(string key) => _events.Value.TryGetValue(key, out var active) && active;

    public IReadOnlyDictionary<string, bool> GetAll() => _events.Value;

    public void SetActive(string key, bool active)
    {
        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            var dbEvent = dbContext.WorldEvents.SingleOrDefault(x => x.Key == key);

            if (dbEvent is null)
                dbContext.WorldEvents.Add(new DbWorldEvent { Key = key, IsActive = active });
            else
                dbEvent.IsActive = active;

            dbContext.SaveChanges();
        }

        _events.Value[key] = active;

        // Resolved lazily: zones resolve this manager while the zone manager is still loading.
        var zoneManager = _serviceProvider.GetRequiredService<IZoneManager>();

        foreach (var zone in zoneManager.Zones.OfType<WorldZone>())
            zone.OnWorldEventChanged(key, active);
    }
}
