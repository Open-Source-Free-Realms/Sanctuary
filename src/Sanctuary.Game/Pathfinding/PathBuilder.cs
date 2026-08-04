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

    public Queue<Vector3>? TryRecompute(Vector3 searchStart, Vector3 goalPosition)
    {
        var path = _pathfinder.FindPath(searchStart, goalPosition);

        if (IsSimilarPath(_lastComputedPath, path))
            return null;

        _lastComputedPath = path;

        var waypoints = new Queue<Vector3>();
        foreach (var node in path)
            waypoints.Enqueue(node.Position);

        waypoints.Enqueue(goalPosition);

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
