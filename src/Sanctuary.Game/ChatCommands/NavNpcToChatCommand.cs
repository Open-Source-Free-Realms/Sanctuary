using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Pathing;
using Sanctuary.Game.Routines;

namespace Sanctuary.Game.ChatCommands;

public class NavNpcToChatCommand : IChatCommand
{
    private const int TestNpcNameId = 437129;
    private const int TestNpcModelId = 3927;
    private const float TestNpcSpeed = 6.25f;
    private const int MaxSearchIterations = 1024;
    private const int DefaultSpawnCount = 1;
    private const int MaxSpawnCount = 100;
    private const float DefaultSpawnRadius = 5f;
    private const float MaxSpawnRadius = 50f;
    private const string RequestRoutine = "NavTestRequest";
    private const string MovementRoutine = "Movement";

    private readonly IChatCommandManager _chatCommandManager;
    private readonly ConcurrentDictionary<ulong, List<ulong>> _testNpcs = new();

    public string KeyWord => "navnpcto";
    public string Usage => "spawn [count] [radius] | moveto [<x> <y> <z>] | chase | stop | despawn";
    public string Description => "Spawns test NPCs in a circle around you and drives them with the new pathfinder: moveto a position (or to you with no position), chase you, stop, or despawn them.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public NavNpcToChatCommand(IChatCommandManager chatCommandManager)
    {
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        return args[0].ToLowerInvariant() switch
        {
            "spawn" => Spawn(invoker, args),
            "moveto" => MoveTo(invoker, args),
            "chase" => Chase(invoker),
            "stop" => Stop(invoker),
            "despawn" => Despawn(invoker),
            _ => false
        };
    }

    private bool Spawn(Player invoker, string[] args)
    {
        var count = DefaultSpawnCount;
        var radius = DefaultSpawnRadius;

        if (args.Length > 1 && !int.TryParse(args[1], out count))
            return false;

        if (args.Length > 2 && !float.TryParse(args[2], out radius))
            return false;

        if (args.Length > 3)
            return false;

        count = Math.Clamp(count, 1, MaxSpawnCount);
        radius = Math.Clamp(radius, 0f, MaxSpawnRadius);

        if (invoker.Zone.NavMeshQuery is null)
        {
            ChatHelper.SendSystemMessage(invoker, "This zone has no navmesh, so the new pathfinder can't run here.");
            return true;
        }

        DespawnAll(invoker);

        var center = invoker.Position.ToVector3();
        var spawned = new List<ulong>(count);

        for (var index = 0; index < count; index++)
        {
            if (!invoker.Zone.TryCreateNpc(null, out var npc))
                break;

            var angle = MathF.Tau * index / count;
            var position = count == 1 ? center : center + new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle)) * radius;
            var facing = count == 1 ? invoker.Rotation : (center - position).ToFlatRotation();

            npc.NameId = TestNpcNameId;
            npc.ModelId = TestNpcModelId;
            npc.Scale = 1f;
            npc.Disposition = 0;
            npc.HideNamePlate = false;
            npc.Speed = TestNpcSpeed;
            npc.Visible = true;

            npc.UpdatePosition(position.ToVector4(), facing);

            spawned.Add(npc.Guid);
        }

        _testNpcs[invoker.Guid] = spawned;

        _chatCommandManager.LogAction(this, invoker, "Spawn Nav Test NPCs", null, $"count={spawned.Count}, radius={radius}");
        ChatHelper.SendSystemMessage(invoker, $"Spawned {spawned.Count} nav test NPC(s) in a {radius:0.0} m circle around you.");
        return true;
    }

    private bool MoveTo(Player invoker, string[] args)
    {
        Vector3 goal;

        if (args.Length == 1)
        {
            goal = invoker.Position.ToVector3();
        }
        else if (args.Length == 4 &&
                 float.TryParse(args[1], out var x) &&
                 float.TryParse(args[2], out var y) &&
                 float.TryParse(args[3], out var z))
        {
            goal = new Vector3(x, y, z);
        }
        else
        {
            return false;
        }

        var npcs = GetTestNpcs(invoker);

        for (var index = 0; index < npcs.Count; index++)
        {
            var npc = npcs[index];
            var report = index == 0;

            npc.Routines.SetRoutine(RequestRoutine, () =>
            {
                if (report)
                    ReportPath(invoker, npc, goal, npcs.Count);

                npc.TryMoveTo(goal);
                return true;
            }, Cadence.Tick);
        }

        return true;
    }

    private bool Chase(Player invoker)
    {
        var npcs = GetTestNpcs(invoker);

        foreach (var npc in npcs)
            npc.Chase(invoker);

        if (npcs.Count > 0)
            ChatHelper.SendSystemMessage(invoker, $"{npcs.Count} nav test NPC(s) are chasing you.");

        return true;
    }

    private bool Stop(Player invoker)
    {
        var npcs = GetTestNpcs(invoker);

        foreach (var npc in npcs)
        {
            npc.Routines.Cancel(RequestRoutine);
            npc.Routines.Cancel(MovementRoutine);
            npc.UpdatePosition(npc.Position, npc.Rotation);
        }

        if (npcs.Count > 0)
            ChatHelper.SendSystemMessage(invoker, $"Stopped {npcs.Count} nav test NPC(s).");

        return true;
    }

    private bool Despawn(Player invoker)
    {
        var count = DespawnAll(invoker);

        ChatHelper.SendSystemMessage(invoker, count > 0 ? $"Despawned {count} nav test NPC(s)." : "You have no nav test NPCs.");
        return true;
    }

    private int DespawnAll(Player invoker)
    {
        if (!_testNpcs.TryRemove(invoker.Guid, out var guids))
            return 0;

        var count = 0;

        foreach (var guid in guids)
        {
            if (!invoker.Zone.TryGetNpc(guid, out var npc))
                continue;

            npc.Dispose();
            count++;
        }

        return count;
    }

    private List<Npc> GetTestNpcs(Player invoker)
    {
        var npcs = new List<Npc>();

        if (_testNpcs.TryGetValue(invoker.Guid, out var guids))
        {
            foreach (var guid in guids)
            {
                if (invoker.Zone.TryGetNpc(guid, out var npc))
                    npcs.Add(npc);
            }
        }

        if (npcs.Count == 0)
            ChatHelper.SendSystemMessage(invoker, "Spawn nav test NPCs first with !navnpcto spawn [count] [radius].");

        return npcs;
    }

    private static void ReportPath(Player invoker, Npc npc, Vector3 goal, int npcCount)
    {
        var zone = npc.Zone;

        if (zone.NavMeshQuery is null)
            return;

        var pathfinder = new Pathfinder(zone.NavMeshQuery, zone.NavMeshFilter, MaxSearchIterations);
        var start = npc.Position.ToVector3();

        if (!pathfinder.TryFindPath(start, goal, out var path))
        {
            ChatHelper.SendSystemMessage(invoker, $"No path from ({start.X:0.0}, {start.Y:0.0}, {start.Z:0.0}) to ({goal.X:0.0}, {goal.Y:0.0}, {goal.Z:0.0}).");
            return;
        }

        var end = path.Waypoints[^1];
        ChatHelper.SendSystemMessage(invoker,
            $"Sending {npcCount} NPC(s). First NPC's path: {path.Waypoints.Count} waypoint(s), {(path.IsComplete ? "complete" : "partial")}, ends at ({end.X:0.0}, {end.Y:0.0}, {end.Z:0.0}), {Vector3.Distance(start, goal):0.0} m straight-line.");
    }
}
