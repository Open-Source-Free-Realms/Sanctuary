using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Game.Pathfinding;


public class PathBuilder
{
    private readonly Pathfinder<MapNode> _pathfinder;
    private List<MapNode> _lastComputedPath = new();

    public PathBuilder(Pathfinder<MapNode> pathfinder)
    {
        _pathfinder = pathfinder;
    }

    public Queue<Vector3>? TryRecompute(Vector3 searchStart, Vector3 goalPosition) =>
        TryRecompute(searchStart, goalPosition, _pathfinder.FindPath(searchStart, goalPosition));

    public Queue<Vector3>? TryRecompute(Vector3 searchStart, Vector3 goalPosition, List<MapNode> path)
    {
        if (IsSimilarPath(_lastComputedPath, path))
            return null;

        _lastComputedPath = path;

        var positions = new List<Vector3>(path.Count + 1);

        foreach (var node in path)
            positions.Add(node.Position);

        positions.Add(goalPosition);

        var start = 0;
        while (start < positions.Count - 1
            && Vector3.DistanceSquared(searchStart, positions[start + 1]) <= Vector3.DistanceSquared(positions[start], positions[start + 1]))
        {
            start++;
        }

        var waypoints = new Queue<Vector3>(positions.Count - start);

        for (var index = start; index < positions.Count; index++)
            waypoints.Enqueue(positions[index]);

        return waypoints;
    }

    private static bool IsSimilarPath(List<MapNode> oldPath, List<MapNode> newPath)
    {
        if (oldPath.Count == 0 || newPath.Count == 0)
            return false;

        var overlap = Math.Min(oldPath.Count, newPath.Count);

        for (var index = 1; index <= overlap; index++)
        {
            if (oldPath[^index].Id != newPath[^index].Id)
                return false;
        }

        return true;
    }
}