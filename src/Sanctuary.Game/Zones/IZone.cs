using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Interactions;
using Sanctuary.Packet.Common;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Scripting;
using Sanctuary.UdpLibrary;
using Sanctuary.Game.Pathfinding;

namespace Sanctuary.Game.Zones;

public interface IZone : IScriptableZone, IDisposable
{
    int DefinitionId { get; }

    ulong? OwnerId { get; }

    Vector4 SpawnPosition { get; }
    Quaternion SpawnRotation { get; }

    string? Sky { get; }

    #region Events

    void OnStart();
    void OnClientIsReady(Player entity);
    void OnClientFinishedLoading(Player entity);

    #endregion

    #region Entities

    IEnumerable<Npc> Npcs { get; }
    IEnumerable<Player> Players { get; }

    bool IsEmpty { get; }

    float TickDeltaSeconds { get; }

    bool TryGetNpc(ulong guid, [MaybeNullWhen(false)] out Npc npc);
    bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player);
    bool TryGetEntity(ulong guid, [MaybeNullWhen(false)] out IEntity entity);

    bool TryAddMount(Mount mount);
    bool TryAddNpc<TNpc>(Func<IZone, ulong, TNpc> createNpc, [MaybeNullWhen(false)] out TNpc npc) where TNpc : Npc;
    bool TryAddPlayer(Player player);

    bool TryCreateNpc([MaybeNullWhen(false)] out Npc npc);
    bool TryCreateNpc(ulong? guid, [MaybeNullWhen(false)] out Npc npc);
    bool TryCreateNpc(ulong? guid, NpcDefinition definition, [MaybeNullWhen(false)] out Npc npc);
    IReadOnlyList<CollectionNodePoolStatus> GetCollectionNodePoolStatuses();
    IReadOnlyList<CollectionNodeSpawnStatus> GetCollectionNodeSpawnStatuses(string? poolKey = null);
    bool TryPlaceCollectionNodeSpawn(string poolKey, Vector4 position, float heading,
        [MaybeNullWhen(false)] out CollectionNodeSpawnDefinition spawn, out bool activated);
    bool TryConfigureCollectionNodePool(string poolKey, int maxActiveNodes, int respawnSeconds,
        out int activeCount, out int targetActiveCount);
    bool TryRemoveCollectionNodeSpawn(int id,
        [MaybeNullWhen(false)] out CollectionNodeSpawnDefinition removedSpawn);
    bool TryRemoveNearestCollectionNodeSpawn(Vector4 position, float radius,
        [MaybeNullWhen(false)] out CollectionNodeSpawnDefinition removedSpawn);
    void CompleteCollectionNode(CollectionNode node);
    bool TryCreateMount(Player rider, MountDefinition definition, [MaybeNullWhen(false)] out Mount mount);
    bool TryCreatePlayer(ulong guid, UdpConnection connection, [MaybeNullWhen(false)] out Player player);

    bool TryRemoveNpc(ulong guid);
    bool TryRemovePlayer(ulong guid);



    #endregion

    #region Zone System

    bool HasEnvironmentData { get; }
    bool IsOutOfBounds(ZoneTile zoneTile);
    bool IsOutOfBounds(Vector4 position);
    ZoneTile GetTileFromPosition(Vector4 position);
    void UpdateEntityZoneTile(IEntity entity, ZoneTile from, ZoneTile to);

    #endregion

    Pathfinder<MapNode>? Pathfinder { get; }

    bool HasGroundData { get; }

    bool TryGetGroundHeight(float x, float z, float fromY, float maxDrop, out float height);

    bool IsWalkable(Vector3 start, Vector3 goal, float maxStepUp, float maxStepDown, out float goalHeight);

    bool TryGetWalkableGoal(Vector3 start, Vector3 goal, float maxStepUp, float maxStepDown, out Vector3 walkableGoal);

    Vector3 GetGroundPosition(Vector3 position, float maxStepUp, float maxDrop);

    bool HasNavMesh { get; }

    bool TryFindNavMeshPath(Vector3 start, Vector3 goal, Queue<Vector3> waypoints);

    bool TryFindClientNavMeshPath(Vector3 start, Vector3 goal, Queue<Vector3> waypoints);

    bool TryFindRoute(Vector3 start, Vector3 goal, Queue<Vector3> waypoints);

    IReadOnlyList<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc);

    NotificationInfo? GetQuestBadge(Player player, Npc npc);

    bool IsHiddenByQuest(Player player, Npc npc);

    int GetQuestEffectId(Player player, Npc npc);

    bool TryFindReachableRoute(Vector3 start, Vector3 goal, Queue<Vector3> waypoints, bool followRoads);
}
