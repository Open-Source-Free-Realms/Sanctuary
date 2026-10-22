using System;
using System.Numerics;

using DotRecast.Core.Numerics;
using DotRecast.Detour;

namespace Sanctuary.Game.Pathing;

public sealed class PathFollower
{
    // NOTE: May want this to live in 'json' like the stuff in the path finder. 
    private static readonly RcVec3f GroundExtents = new(0.5f, 2f, 0.5f);
    private static readonly DtQueryDefaultFilter GroundFilter = new();

    private readonly DtNavMeshQuery? _query;

    private int _waypoint;

    public NavPath? Path { get; private set; }
    public bool HasPath => Path is not null;

    private float _stopTolerance;

    public PathFollower(DtNavMeshQuery? query)
    {
        _query = query;
    }

    public void SetPath(NavPath path, float stopTolerance = 0f)
    {
        Path = path;
        _waypoint = 0;
        _stopTolerance = stopTolerance;
    }

    public void Stop()
    {
        Path = null;
        _waypoint = 0;
        _stopTolerance = 0f;
    }

    public Vector3 Advance(Vector3 position, float distance, out bool arrived)
    {
        arrived = false;

        var path = Path;

        if (path is null)
            return position;

        while (distance > 0f && _waypoint < path.Waypoints.Count)
        {
            var waypoint = path.Waypoints[_waypoint];
            var toWaypoint = waypoint - position;
            var length = new Vector2(toWaypoint.X, toWaypoint.Z).Length();

            var isGoal = _waypoint == path.Waypoints.Count - 1;
            var reach = isGoal ? MathF.Max(0f, length - _stopTolerance) : length;

            if (reach > distance)
            {
                position += toWaypoint * (distance / length);
                break;
            }

            if (isGoal)
            {
                if (length > 0f)
                    position += toWaypoint * (reach / length);

                arrived = true;
                break;
            }

            position = waypoint;
            distance -= length;
            _waypoint++;
        }

        if (arrived)
            Stop();

        return SnapToGround(position);
    }

    private Vector3 SnapToGround(Vector3 position)
    {
        if (_query is null)
            return position;

        var point = position.ToRecast();

        _query.FindNearestPoly(point, GroundExtents, GroundFilter, out var polygon, out _, out _);

        if (polygon != 0 && _query.GetPolyHeight(polygon, point, out var height).Succeeded())
            position.Y = height;

        return position;
    }
}
