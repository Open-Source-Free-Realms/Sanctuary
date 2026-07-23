using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Scripting;
using Sanctuary.UdpLibrary;

namespace Sanctuary.Game.Zones;

[DebuggerDisplay("{Name} ({Id})")]
public abstract class BaseZone : IZone, IDisposable
{
    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly BaseZoneDefinition _zoneDefinition;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private const int VisibleTileRadius = 2;
    private readonly Dictionary<int, ZoneTile> _tiles;

    private static ulong _nextNpcGuid = NpcBaseGuid;

    private readonly ConcurrentDictionary<ulong, Npc> _npcs = new();
    private readonly ConcurrentDictionary<ulong, Player> _players = new();
    private readonly ConcurrentDictionary<ulong, IEntity> _entities = new();
    private readonly object _collectionNodeLock = new();
    private readonly PriorityQueue<CollectionNodePoolRefill, long> _collectionNodeRefills = new();

    private const int FrameRate = 10;
    private const float TickRate = 1000f / FrameRate;
    private const ulong NpcBaseGuid = 100_000_000_000u;

    private readonly record struct CollectionNodePoolRefill(string PoolKey, int CollectedHardPointId);

    private readonly PeriodicTimer _updateEveryTickTimer = new(TimeSpan.FromMilliseconds(TickRate));
    private readonly PeriodicTimer _updateEverySecondTimer = new(TimeSpan.FromSeconds(1));

    public int Id { get; init; }
    public int DefinitionId => _zoneDefinition.Id;
    public string Name => _zoneDefinition.Name;
    public ILogger Logger => _logger;

    public Vector4 SpawnPosition => _zoneDefinition.SpawnPosition;
    public Quaternion SpawnRotation => _zoneDefinition.SpawnRotation;

    public IEnumerable<Npc> Npcs => _npcs.Values;
    public IEnumerable<Player> Players => _players.Values;

    private ScriptContext? _scriptContext;

    protected BaseZone(BaseZoneDefinition zoneDefinition, IServiceProvider serviceProvider)
    {
        _zoneDefinition = zoneDefinition;
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger($"Zone {Name} ({Id})");

        var scriptManager = serviceProvider.GetRequiredService<IScriptManager>();

        _scriptContext = scriptManager.GetContextForZone(this);

        _tiles = GenerateTiles();

        foreach (var tile in _tiles)
        {
            ArgumentNullException.ThrowIfNull(tile.Value.Entities);
            ArgumentNullException.ThrowIfNull(tile.Value.VisibleTiles);
        }

        Task.Factory.StartNew(UpdateEveryTickAsync, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task.Factory.StartNew(UpdateEverySecondAsync, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    #region Events

    public virtual void OnStart()
    {
        ActivateCollectionNodePools();

        // fire and forget. safe since CallFunctionAsync does not throw.
        _ = _scriptContext?.CallFunctionAsync("onStart", this).AsTask();
    }

    public virtual void OnClientIsReady(Player player)
    {
    }

    public virtual void OnClientFinishedLoading(Player player)
    {
    }

    #endregion

    #region Scripting

    public bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading)
    {
        if (npcGuid.HasValue)
        {
            if (_npcs.ContainsKey(npcGuid.Value))
            {
                _logger.LogWarning("Failed to spawn NPC {NpcId} with GUID {NpcGuid}: GUID already exists.", npcId, npcGuid.Value);
                return false;
            }
        }

        var definition = _resourceManager.Npcs.Values.FirstOrDefault(n => n.Id == npcId);
        if (definition is null)
        {
            _logger.LogWarning("Failed to spawn NPC {NpcId}: No definition found.", npcId);
            return false;
        }

        if (!TryCreateNpc(npcGuid, definition, out var npc))
        {
            _logger.LogWarning("Failed to spawn NPC {NpcId}: Could not create NPC instance.", npcId);
            return false;
        }

        var position = new Vector4(x, y, z, 1f);
        var rotation = new Quaternion(MathF.Sin(heading), 0f, MathF.Cos(heading), 0f);

        npc.UpdatePosition(position, rotation);

        return true;
    }

    #endregion

    #region Entities

    public bool TryGetNpc(ulong guid, [MaybeNullWhen(false)] out Npc npc)
    {
        return _npcs.TryGetValue(guid, out npc);
    }

    public bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player)
    {
        return _players.TryGetValue(guid, out player);
    }

    public bool TryGetEntity(ulong guid, [MaybeNullWhen(false)] out IEntity entity)
    {
        return _entities.TryGetValue(guid, out entity);
    }

    public bool TryAddMount(Mount mount)
    {
        return _npcs.TryAdd(mount.Guid, mount) && _entities.TryAdd(mount.Guid, mount);
    }

    public bool TryAddPlayer(Player player)
    {
        return _players.TryAdd(player.Guid, player) && _entities.TryAdd(player.Guid, player);
    }

    public bool TryCreateNpc(ulong? guid, [MaybeNullWhen(false)] out Npc npc)
    {
        npc = new Npc(this)
        {
            Guid = GetNpcGuid(guid)
        };

        return _npcs.TryAdd(npc.Guid, npc) && _entities.TryAdd(npc.Guid, npc);
    }

    public bool TryCreateNpc(ulong? guid, NpcDefinition definition, [MaybeNullWhen(false)] out Npc npc)
    {
        var scale = 1f;

        if (_resourceManager.Models.TryGetValue(definition.ModelId, out var model) && model.Scale != 0f)
            scale = model.Scale;

        npc = new Npc(this)
        {
            Guid = GetNpcGuid(guid),
            NameId = definition.NameId,
            Name = definition.Name,
            ModelId = definition.ModelId,
            TextureAlias = definition.TextureAlias,
            Scale = scale,
            Static = true,
            Visible = true
        };

        return true;
    }

    public IReadOnlyList<CollectionNodePoolStatus> GetCollectionNodePoolStatuses()
    {
        lock (_collectionNodeLock)
        {
            return _resourceManager.CollectionNodePools.Values
                .Where(pool => pool.ZoneDefinitionId == DefinitionId)
                .OrderBy(pool => pool.Key)
                .Select(pool =>
                {
                    var hardPointCount = _resourceManager.CollectionNodeSpawns.Values.Count(
                        spawn => spawn.Pool == pool.Key);
                    var activeCount = _npcs.Values.OfType<CollectionNode>().Count(
                        node => node.PoolDefinition.Key == pool.Key);

                    return new CollectionNodePoolStatus(
                        pool.Key,
                        pool.NodeType,
                        activeCount,
                        hardPointCount,
                        pool.GetTargetActiveCount(hardPointCount),
                        pool.RespawnSeconds);
                })
                .ToArray();
        }
    }

    public IReadOnlyList<CollectionNodeSpawnStatus> GetCollectionNodeSpawnStatuses(string? poolKey = null)
    {
        lock (_collectionNodeLock)
        {
            poolKey = poolKey?.Trim().ToLowerInvariant();
            var activeIds = _npcs.Values
                .OfType<CollectionNode>()
                .Select(node => node.SpawnDefinition.Id)
                .ToHashSet();
            var zonePoolKeys = _resourceManager.CollectionNodePools.Values
                .Where(pool => pool.ZoneDefinitionId == DefinitionId)
                .Select(pool => pool.Key)
                .ToHashSet();

            return _resourceManager.CollectionNodeSpawns.Values
                .Where(spawn => zonePoolKeys.Contains(spawn.Pool) &&
                    (poolKey is null || spawn.Pool == poolKey))
                .OrderBy(spawn => spawn.Id)
                .Select(spawn => new CollectionNodeSpawnStatus(
                    spawn.Id, spawn.Pool, spawn.SpawnPosition, activeIds.Contains(spawn.Id)))
                .ToArray();
        }
    }

    public bool TryPlaceCollectionNodeSpawn(string poolKey, Vector4 position, float heading,
        [MaybeNullWhen(false)] out CollectionNodeSpawnDefinition spawn, out bool activated)
    {
        lock (_collectionNodeLock)
        {
            spawn = null;
            activated = false;

            if (string.IsNullOrWhiteSpace(poolKey) ||
                !_resourceManager.CollectionNodePools.TryGetValue(poolKey.Trim().ToLowerInvariant(), out var poolDefinition) ||
                poolDefinition.ZoneDefinitionId != DefinitionId ||
                !_resourceManager.CollectionNodeTypes.TryGetValue(poolDefinition.NodeType, out var typeDefinition))
            {
                return false;
            }

            position.Y += typeDefinition.PlacementYOffset;

            if (!_resourceManager.CollectionNodeSpawns.TryAddPersistent(
                poolDefinition.Key, DefinitionId, position, heading, out spawn))
            {
                return false;
            }

            activated = TryActivateCollectionNodeSpawn(spawn, out _);
            return true;
        }
    }

    public bool TryConfigureCollectionNodePool(string poolKey, int maxActiveNodes, int respawnSeconds,
        out int activeCount, out int targetActiveCount)
    {
        lock (_collectionNodeLock)
        {
            activeCount = 0;
            targetActiveCount = 0;

            if (string.IsNullOrWhiteSpace(poolKey) ||
                !_resourceManager.CollectionNodePools.TryGetValue(poolKey.Trim().ToLowerInvariant(), out var poolDefinition) ||
                poolDefinition.ZoneDefinitionId != DefinitionId ||
                !_resourceManager.CollectionNodePools.TryUpdatePersistent(
                    poolDefinition.Key, maxActiveNodes, respawnSeconds))
            {
                return false;
            }

            activeCount = ReconcileCollectionNodePool(poolDefinition.Key);
            var hardPointCount = _resourceManager.CollectionNodeSpawns.Values.Count(
                spawn => spawn.Pool == poolDefinition.Key);
            targetActiveCount = poolDefinition.GetTargetActiveCount(hardPointCount);
            return true;
        }
    }

    public bool TryRemoveCollectionNodeSpawn(int id,
        [MaybeNullWhen(false)] out CollectionNodeSpawnDefinition removedSpawn)
    {
        lock (_collectionNodeLock)
        {
            removedSpawn = null;

            if (!_resourceManager.CollectionNodeSpawns.TryGetValue(id, out var spawn) ||
                !_resourceManager.CollectionNodePools.TryGetValue(spawn.Pool, out var poolDefinition) ||
                poolDefinition.ZoneDefinitionId != DefinitionId ||
                !_resourceManager.CollectionNodeSpawns.TryRemovePersistent(id))
            {
                return false;
            }

            var activeNode = _npcs.Values
                .OfType<CollectionNode>()
                .FirstOrDefault(node => node.SpawnDefinition.Id == id);

            activeNode?.Dispose();
            ReconcileCollectionNodePool(spawn.Pool);
            removedSpawn = spawn;
            return true;
        }
    }

    public bool TryRemoveNearestCollectionNodeSpawn(Vector4 position, float radius,
        [MaybeNullWhen(false)] out CollectionNodeSpawnDefinition removedSpawn)
    {
        lock (_collectionNodeLock)
        {
            removedSpawn = null;

            if (radius <= 0)
                return false;

            var position3 = new Vector3(position.X, position.Y, position.Z);
            var node = _npcs.Values
                .OfType<CollectionNode>()
                .Where(candidate => _resourceManager.CollectionNodeSpawns.ContainsKey(candidate.SpawnDefinition.Id))
                .Select(candidate => new
                {
                    Node = candidate,
                    DistanceSquared = Vector3.DistanceSquared(
                        new Vector3(candidate.Position.X, candidate.Position.Y, candidate.Position.Z), position3)
                })
                .Where(candidate => candidate.DistanceSquared <= radius * radius)
                .OrderBy(candidate => candidate.DistanceSquared)
                .Select(candidate => candidate.Node)
                .FirstOrDefault();

            return node is not null && TryRemoveCollectionNodeSpawn(node.SpawnDefinition.Id, out removedSpawn);
        }
    }

    private bool TryActivateCollectionNodeSpawn(CollectionNodeSpawnDefinition spawnDefinition,
        [MaybeNullWhen(false)] out CollectionNode node)
    {
        node = null;

        if (!_resourceManager.CollectionNodeSpawns.ContainsKey(spawnDefinition.Id) ||
            !_resourceManager.CollectionNodePools.TryGetValue(spawnDefinition.Pool, out var poolDefinition) ||
            poolDefinition.ZoneDefinitionId != DefinitionId ||
            !_resourceManager.CollectionNodeTypes.TryGetValue(poolDefinition.NodeType, out var typeDefinition) ||
            _npcs.Values.OfType<CollectionNode>().Any(active => active.SpawnDefinition.Id == spawnDefinition.Id))
        {
            return false;
        }

        var hardPointCount = _resourceManager.CollectionNodeSpawns.Values.Count(spawn => spawn.Pool == poolDefinition.Key);
        var activeCount = _npcs.Values.OfType<CollectionNode>().Count(active => active.PoolDefinition.Key == poolDefinition.Key);

        if (activeCount >= poolDefinition.GetTargetActiveCount(hardPointCount))
            return false;

        return TryCreateCollectionNode(typeDefinition, poolDefinition, spawnDefinition, out node);
    }

    protected int ActivateCollectionNodePools()
    {
        var activated = 0;
        var pools = _resourceManager.CollectionNodePools.Values
            .Where(pool => pool.ZoneDefinitionId == DefinitionId)
            .ToArray();

        foreach (var pool in pools)
            activated += RefillCollectionNodePool(pool, int.MaxValue);

        _logger.LogInformation("Activated {count} collection node(s) across {poolCount} pool(s).",
            activated, pools.Length);

        return activated;
    }

    private int ReconcileCollectionNodePool(string poolKey)
    {
        if (!_resourceManager.CollectionNodePools.TryGetValue(poolKey, out var poolDefinition))
            return 0;

        lock (_collectionNodeLock)
        {
            var activeNodes = _npcs.Values
                .OfType<CollectionNode>()
                .Where(node => node.PoolDefinition.Key == poolDefinition.Key)
                .ToList();
            var hardPointCount = _resourceManager.CollectionNodeSpawns.Values.Count(spawn => spawn.Pool == poolDefinition.Key);
            var targetActiveCount = poolDefinition.GetTargetActiveCount(hardPointCount);

            while (activeNodes.Count > targetActiveCount)
            {
                var index = Random.Shared.Next(activeNodes.Count);
                activeNodes[index].Dispose();
                activeNodes.RemoveAt(index);
            }

            RefillCollectionNodePool(poolDefinition, int.MaxValue);
            return _npcs.Values.OfType<CollectionNode>().Count(node => node.PoolDefinition.Key == poolDefinition.Key);
        }
    }

    public void CompleteCollectionNode(CollectionNode node)
    {
        lock (_collectionNodeLock)
        {
            if (!_npcs.ContainsKey(node.Guid))
                return;

            node.DisposeAfterCollection();

            if (!_resourceManager.CollectionNodePools.TryGetValue(node.PoolDefinition.Key, out var poolDefinition) ||
                poolDefinition.ZoneDefinitionId != DefinitionId)
            {
                return;
            }

            var dueTimestamp = Stopwatch.GetTimestamp() +
                (long)(poolDefinition.RespawnSeconds * (double)Stopwatch.Frequency);
            _collectionNodeRefills.Enqueue(
                new CollectionNodePoolRefill(poolDefinition.Key, node.SpawnDefinition.Id), dueTimestamp);
        }
    }

    private bool TryCreateCollectionNode(CollectionNodeTypeDefinition typeDefinition,
        CollectionNodePoolDefinition poolDefinition, CollectionNodeSpawnDefinition spawnDefinition,
        [MaybeNullWhen(false)] out CollectionNode node)
    {
        node = new CollectionNode(this, typeDefinition, poolDefinition, spawnDefinition)
        {
            Guid = _nextNpcGuid++,
            Name = typeDefinition.Name,
            ModelId = typeDefinition.ModelId,
            Scale = typeDefinition.Scale,
            CompositeEffectId = typeDefinition.CompositeEffectId,
            InteractRange = typeDefinition.InteractRange,
            CursorId = typeDefinition.CursorId,
            Static = true,
            Visible = true
        };

        if (!_npcs.TryAdd(node.Guid, node) || !_entities.TryAdd(node.Guid, node))
            return false;

        node.UpdatePosition(spawnDefinition.SpawnPosition, spawnDefinition.SpawnRotation);
        return true;
    }

    private int RefillCollectionNodePool(CollectionNodePoolDefinition poolDefinition, int maximumToActivate,
        int? avoidHardPointId = null)
    {
        lock (_collectionNodeLock)
        {
            if (_cancellationTokenSource.IsCancellationRequested || poolDefinition.ZoneDefinitionId != DefinitionId ||
                !_resourceManager.CollectionNodeTypes.TryGetValue(poolDefinition.NodeType, out var typeDefinition))
            {
                return 0;
            }

            var activeNodes = _npcs.Values
                .OfType<CollectionNode>()
                .Where(node => node.PoolDefinition.Key == poolDefinition.Key)
                .ToArray();
            var activeHardPointIds = activeNodes
                .Select(node => node.SpawnDefinition.Id)
                .ToHashSet();
            var selected = poolDefinition.SelectSpawnsToActivate(
                _resourceManager.CollectionNodeSpawns.Values, activeHardPointIds, maximumToActivate, avoidHardPointId);
            var activated = 0;

            foreach (var spawn in selected)
            {
                if (TryCreateCollectionNode(typeDefinition, poolDefinition, spawn, out _))
                    activated++;
            }

            return activated;
        }
    }

    private void ProcessCollectionNodeRefills()
    {
        lock (_collectionNodeLock)
        {
            var now = Stopwatch.GetTimestamp();

            while (_collectionNodeRefills.TryPeek(out var refill, out var dueTimestamp) && dueTimestamp <= now)
            {
                _collectionNodeRefills.Dequeue();

                if (_resourceManager.CollectionNodePools.TryGetValue(refill.PoolKey, out var poolDefinition))
                    RefillCollectionNodePool(poolDefinition, 1, refill.CollectedHardPointId);
            }
        }
    }

    public bool TryCreateMount(Player rider, MountDefinition definition, [MaybeNullWhen(false)] out Mount mount)
    {
        mount = new Mount(this, rider, definition)
        {
            Guid = _nextNpcGuid++
        };

        return _npcs.TryAdd(mount.Guid, mount) && _entities.TryAdd(mount.Guid, mount);
    }

    public bool TryCreatePlayer(ulong guid, UdpConnection connection, [MaybeNullWhen(false)] out Player player)
    {
        player = new Player(this, connection, _resourceManager)
        {
            Guid = guid
        };

        return _players.TryAdd(player.Guid, player) && _entities.TryAdd(player.Guid, player);
    }

    public bool TryRemoveNpc(ulong guid)
    {
        return _npcs.TryRemove(guid, out _) && _entities.TryRemove(guid, out _);
    }

    public bool TryRemovePlayer(ulong guid)
    {
        return _players.TryRemove(guid, out _) && _entities.TryRemove(guid, out _);
    }

    #endregion

    #region Zone System

    private Dictionary<int, ZoneTile> GenerateTiles()
    {
        var tiles = new Dictionary<int, ZoneTile>();

        // Generate all tiles
        for (var longitude = _zoneDefinition.StartLongitude; longitude < _zoneDefinition.EndLongitude; longitude++)
        {
            for (var latitude = _zoneDefinition.StartLatitude; latitude < _zoneDefinition.EndLatitude; latitude++)
            {
                var tileHash = ZoneTile.GetHash(longitude, latitude);

                tiles.Add(tileHash, new ZoneTile(longitude, latitude));
            }
        }

        // Calcualte visible tiles
        for (var rootLongitude = _zoneDefinition.StartLongitude; rootLongitude < _zoneDefinition.EndLongitude; rootLongitude++)
        {
            for (var rootLatitude = _zoneDefinition.StartLatitude; rootLatitude < _zoneDefinition.EndLatitude; rootLatitude++)
            {
                var rootTileHash = ZoneTile.GetHash(rootLongitude, rootLatitude);

                var rootTile = tiles[rootTileHash];

                for (var visibleLongitude = rootTile.Longitude - VisibleTileRadius; visibleLongitude <= rootTile.Longitude + VisibleTileRadius; visibleLongitude++)
                {
                    for (var visibleLatitude = rootTile.Latitude - VisibleTileRadius; visibleLatitude <= rootTile.Latitude + VisibleTileRadius; visibleLatitude++)
                    {
                        var visibleTileHash = ZoneTile.GetHash(visibleLongitude, visibleLatitude);

                        if (tiles.TryGetValue(visibleTileHash, out var visibleTile))
                            rootTile.VisibleTiles.Add(visibleTile);
                    }
                }
            }
        }

        return tiles;
    }

    public ZoneTile GetTileFromPosition(Vector4 position)
    {
        var tileLatitude = (int)Math.Floor(position.X / _zoneDefinition.TileSize);
        var tileLongitude = (int)Math.Floor(position.Z / _zoneDefinition.TileSize);

        return GetTileFromCoordinate(tileLongitude, tileLatitude);
    }

    private ZoneTile GetTileFromCoordinate(int longitude, int latitude)
    {
        if (longitude < _zoneDefinition.StartLongitude ||
            longitude >= _zoneDefinition.EndLongitude)
            return ZoneTile.Empty;

        if (latitude < _zoneDefinition.StartLatitude ||
            latitude >= _zoneDefinition.EndLatitude)
            return ZoneTile.Empty;

        var tileHash = ZoneTile.GetHash(longitude, latitude);

        if (!_tiles.TryGetValue(tileHash, out var zoneTile))
            return ZoneTile.Empty;

        return zoneTile;
    }

    public void UpdateEntityZoneTile(IEntity entity, ZoneTile from, ZoneTile to)
    {
        from.Entities.TryRemove(entity.Guid, out _);

        var oldVisibleTiles = from.VisibleTiles;
        var newVisibleTiles = to.VisibleTiles;

        var tilesToAdd = newVisibleTiles.Except(oldVisibleTiles);
        var tilesToRemove = oldVisibleTiles.Except(newVisibleTiles);

        AddEntityToZoneTiles(entity, tilesToAdd);
        RemoveEntityFromZoneTiles(entity, tilesToRemove);

        to.Entities.TryAdd(entity.Guid, entity);
    }

    private void AddEntityToZoneTiles(IEntity entity, IEnumerable<ZoneTile> zoneTiles)
    {
        var npcsToAdd = new List<Npc>();
        var playersToAdd = new List<Player>();

        foreach (var zoneTile in zoneTiles)
        {
            foreach (var zoneTileEntity in zoneTile.Entities)
            {
                if (!zoneTileEntity.Value.Visible || entity == zoneTileEntity.Value)
                    continue;

                switch (zoneTileEntity.Value)
                {
                    case Npc zoneTileNpc:
                        {
                            npcsToAdd.Add(zoneTileNpc);

                            if (entity.Visible)
                            {
                                switch (entity)
                                {
                                    case Npc npc:
                                        break;

                                    case Player player:
                                        zoneTileNpc.OnAddVisiblePlayers(player);
                                        break;
                                }
                            }
                        }
                        break;

                    case Player zoneTilePlayer:
                        {
                            playersToAdd.Add(zoneTilePlayer);

                            if (entity.Visible)
                            {
                                switch (entity)
                                {
                                    case Npc npc:
                                        {
                                            zoneTilePlayer.OnAddVisibleNpcs(npc);
                                        }
                                        break;

                                    case Player player:
                                        zoneTilePlayer.OnAddVisiblePlayers(player);
                                        break;
                                }
                            }
                        }
                        break;
                }
            }
        }

        entity.OnAddVisibleNpcs(npcsToAdd);
        entity.OnAddVisiblePlayers(playersToAdd);
    }

    private void RemoveEntityFromZoneTiles(IEntity entity, IEnumerable<ZoneTile> zoneTiles)
    {
        var npcsToRemove = new List<Npc>();
        var playersToRemove = new List<Player>();

        foreach (var zoneTile in zoneTiles)
        {
            foreach (var zoneTileEntity in zoneTile.Entities)
            {
                if (!zoneTileEntity.Value.Visible || entity == zoneTileEntity.Value)
                    continue;

                switch (zoneTileEntity.Value)
                {
                    case Npc zoneTileNpc:
                        {
                            npcsToRemove.Add(zoneTileNpc);

                            if (entity.Visible)
                            {
                                switch (entity)
                                {
                                    case Npc npc:
                                        break;

                                    case Player player:
                                        zoneTileNpc.OnRemoveVisiblePlayers(player);
                                        break;
                                }
                            }
                        }
                        break;

                    case Player zoneTilePlayer:
                        {
                            playersToRemove.Add(zoneTilePlayer);

                            if (entity.Visible)
                            {
                                switch (entity)
                                {
                                    case Npc npc:
                                        {
                                            if (zoneTilePlayer.Mount is not null && zoneTilePlayer.Mount == npc)
                                                continue;

                                            zoneTilePlayer.OnRemoveVisibleNpcs(npc);
                                        }
                                        break;

                                    case Player player:
                                        zoneTilePlayer.OnRemoveVisiblePlayers(player);
                                        break;
                                }
                            }
                        }
                        break;
                }
            }
        }

        entity.OnRemoveVisibleNpcs(npcsToRemove);
        entity.OnRemoveVisiblePlayers(playersToRemove);
    }

    #endregion

    #region Update

    private async Task UpdateEveryTickAsync()
    {
        while (await _updateEveryTickTimer.WaitForNextTickAsync() && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                ProcessCollectionNodeRefills();

                foreach (var entity in _entities)
                {
                    if (entity.Value is Npc { Static: true })
                        continue;

                    entity.Value.UpdateEveryTick();
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{Name} ({Id}) - Zone Exception");
            }
        }
    }

    private async Task UpdateEverySecondAsync()
    {
        while (await _updateEverySecondTimer.WaitForNextTickAsync() && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                foreach (var entity in _entities)
                {
                    if (entity.Value is Npc { Static: true })
                        continue;

                    entity.Value.UpdateEverySecond();
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{Name} ({Id}) - Zone Exception");
            }
        }
    }

    #endregion

    private ulong GetNpcGuid(ulong? guid)
    {
        if (guid.HasValue)
        {
            _nextNpcGuid = Math.Max(_nextNpcGuid, guid.Value + 1);
            return guid.Value;
        }

        return _nextNpcGuid++;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();

        lock (_collectionNodeLock)
            _collectionNodeRefills.Clear();

        _tiles.Clear();

        _npcs.Clear();
        _players.Clear();
    }
}
