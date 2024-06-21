using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using Sanctuary.UdpLibrary;
using Sanctuary.Game.Entities;
using Sanctuary.Core.Extensions;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game;

public sealed class Zone : IZone, IDisposable
{
    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// [Longitude][Latitude]
    /// </summary>
    // private readonly ZoneTile[][] _tiles;
    // private static readonly int TileRadius = 2;
    // private static readonly float TileApothem = 32f;

    public IEnumerable<IEntity> Entities => _entities.Values;
    private readonly ConcurrentDictionary<ulong, IEntity> _entities = new();

    private static readonly int FrameRate = 10;
    private static readonly float UpdateRate = 1000f / FrameRate;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly PeriodicTimer _updateTimer = new(TimeSpan.FromMilliseconds(UpdateRate));
    private readonly PeriodicTimer _updateEverySecondTimer = new(TimeSpan.FromSeconds(1));

    public ZoneDefinition Definition { get; }

    public Zone(ZoneDefinition zoneDefinition, ILoggerFactory loggerFactory, IResourceManager resourceManager, IServiceProvider serviceProvider)
    {
        Definition = zoneDefinition;

        _resourceManager = resourceManager;
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<Zone>();

        // _tiles = GenerateTiles(zoneDefinition.Gzne);

        Task.Factory.StartNew(UpdateAsync, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task.Factory.StartNew(UpdateEverySecondAsync, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void RemoveEntity(IEntity entity)
    {
        _entities.TryRemove(entity.Guid, out _);
    }

    public bool TryCreateNpc([MaybeNullWhen(false)] out Npc npc)
    {
        npc = new Npc()
        {
            Zone = this
        };

        return _entities.TryAdd(npc.Guid, npc);
    }

    public bool TryCreateMount(Player rider, MountDefinition definition, [MaybeNullWhen(false)] out Mount mount)
    {
        mount = new Mount(rider, definition)
        {
            Zone = this
        };

        return _entities.TryAdd(mount.Guid, mount);
    }

    public bool TryCreatePlayer(ulong guid, UdpConnection connection, [MaybeNullWhen(false)] out Player player)
    {
        player = null;

        if (_entities.ContainsKey(guid))
            return false;

        player = new Player(guid, connection, _resourceManager)
        {
            Zone = this
        };

        return _entities.TryAdd(guid, player);
    }

    /* private ZoneTile[][] GenerateTiles(GzneDefinition gzne)
    {
        var latitudeTiles = Math.Abs(gzne.StartLatitude - gzne.EndLatitude);
        var longitudeTiles = Math.Abs(gzne.StartLongitude - gzne.EndLongitude);

        var tiles = new ZoneTile[longitudeTiles][];

        for (var i = 0; i < longitudeTiles; i++)
            tiles[i] = new ZoneTile[latitudeTiles];

        for (var longitude = 0; longitude < latitudeTiles; longitude++)
            for (var latitude = 0; latitude < longitudeTiles; latitude++)
                tiles[longitude][latitude] = new ZoneTile(longitude + gzne.StartLongitude, latitude + gzne.StartLatitude);

        return tiles;
    }

    private ZoneTile? GetTileFromPosition(Vector4 position)
    {
        var gzne = Definition.Gzne;

        var tileLatitude = (int)Math.Floor((position.X - TileApothem) / gzne.WorldMin);
        var tileLongitude = (int)Math.Floor((position.Z - TileApothem) / gzne.WorldMin);

        return GetTileFromCoordinate(tileLongitude, tileLatitude);
    }

    private ZoneTile? GetTileFromCoordinate(int longitude, int latitude)
    {
        var gzne = Definition.Gzne;

        if (longitude < gzne.StartLatitude || longitude >= gzne.StartLatitude + gzne.EndLatitude)
            return null;

        if (latitude < gzne.StartLongitude || latitude >= gzne.StartLongitude + gzne.EndLongitude)
            return null;

        return _tiles[longitude][latitude];
    }

    private IEnumerable<ZoneTile> GetTilesFromPosition(Vector4 position, int radius)
    {
        var centerTile = GetTileFromPosition(position);

        if (centerTile is null)
            return Enumerable.Empty<ZoneTile>();

        var tiles = new List<ZoneTile>();

        for (var longitude = centerTile.Longitude - radius; longitude <= centerTile.Longitude + radius; longitude++)
        {
            for (var latitude = centerTile.Latitude - radius; latitude <= centerTile.Latitude + radius; latitude++)
            {
                var tile = GetTileFromCoordinate(longitude, latitude);

                if (tile is not null)
                    tiles.Add(tile);
            }
        }

        return tiles;
    } */

    private async Task UpdateAsync()
    {
        while (await _updateTimer.WaitForNextTickAsync() && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                var entities = _entities.ToFrozenDictionary();

                foreach (var entity in entities)
                    entity.Value.Update();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{Definition.Name} Zone Exception");
            }
        }
    }

    private async Task UpdateEverySecondAsync()
    {
        while (await _updateEverySecondTimer.WaitForNextTickAsync() && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                var entities = _entities.ToFrozenDictionary();

                UpdateVisibleEntities(entities.Values);

                foreach (var entity in entities)
                {
                    entity.Value.UpdateEverySecond();
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{Definition.Name} Zone Exception");
            }
        }
    }

    private void UpdateVisibleEntities(IEnumerable<IEntity> entities)
    {
        foreach (var entity in entities)
        {
            var zoneVisibleEntities = entities.Where(x => x.Guid != entity.Guid && x.Visible && x.Position.IsInRange(entity.Position, 135f));

            var entityVisibleEntities = entity.VisibleEntities.Values.ToFrozenSet();

            var entitiesToAdd = zoneVisibleEntities.Except(entityVisibleEntities);
            var entitiesToRemove = entityVisibleEntities.Except(zoneVisibleEntities);

            foreach (var entityToAdd in entitiesToAdd)
                entity.OnEntityAdd(entityToAdd);

            foreach (var entityToRemove in entitiesToRemove)
                entity.OnEntityRemove(entityToRemove);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();

        _entities.Clear();
    }
}