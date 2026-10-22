using System.Collections.Generic;
using System.Numerics;

using DotRecast.Core.Numerics;
using DotRecast.Detour;

namespace Sanctuary.Game.Pathing;


public sealed class Pathfinder
{
    // NOTE: Perhaps these need to come from 'json', especially if they becomes
    // specific per entity or something
    private const int MaxPathPolygons = 256;
    private const int MaxStraightPathPoints = 64;
    private const int MaxRaycastPolygons = 256;

    private static readonly RcVec3f NearestPolygonExtents = new(2f, 4f, 2f);

    private readonly int _maxSearchIterations;


    private DtNavMeshQuery Query { get; }

    // NOTE: We may want different filters selectable by entity. For now, it'll all be the same.
    private IDtQueryFilter Filter { get; }

    public Pathfinder(DtNavMeshQuery query, IDtQueryFilter filter, int maxSearchIterations)
    {
        Query = query;
        Filter = filter;
        _maxSearchIterations = maxSearchIterations;
    }

    public bool TryFindPath(Vector3 startPosition, Vector3 goalPosition, out NavPath path)
    {
        path = new NavPath([], false);

        Query.FindNearestPoly(startPosition.ToRecast(), NearestPolygonExtents, Filter, out var startPolygon, out var start, out _);
        Query.FindNearestPoly(goalPosition.ToRecast(), NearestPolygonExtents, Filter, out var goalPolygon, out var goal, out _);



        if (startPolygon == 0 || goalPolygon == 0)
            return false;


        // NOTE: There's basiscally no point in running A* (below) if there is a straight line to the path. 
        // TODO: Play around with pathfinding and make sure the filter actually fits Free Realms' retail behavior!
        var raycastPolygons = new long[MaxRaycastPolygons];

        if (Query.Raycast(startPolygon, start, goal, Filter, out var hit, out _, raycastPolygons, out var raycastCount, MaxRaycastPolygons).Succeeded()
            && hit == float.MaxValue
            && raycastCount > 0
            && raycastPolygons[raycastCount - 1] == goalPolygon)
        {
            path = new NavPath([goal.ToVector3()], true);
            return true;
        }

        if (Query.InitSlicedFindPath(startPolygon, goalPolygon, start, goal, Filter, 0).Failed())
            return false;

        // TODO: Maybe log the result.
        var searchStatus = Query.UpdateSlicedFindPath(_maxSearchIterations, out _);

        var polygons = new long[MaxPathPolygons];

        if (Query.FinalizeSlicedFindPath(polygons, out var polygonCount, MaxPathPolygons).Failed() || polygonCount == 0)
            return false;

        var isComplete = !searchStatus.InProgress() && polygons[polygonCount - 1] == goalPolygon;

        var end = goal;

        if (!isComplete)
            Query.ClosestPointOnPoly(polygons[polygonCount - 1], goal, out end, out _);

        var straightPath = new DtStraightPath[MaxStraightPathPoints];

        if (Query.FindStraightPath(start, end, polygons, polygonCount, straightPath, out var pointCount, MaxStraightPathPoints, 0).Failed())
            return false;

        var waypoints = new List<Vector3>(pointCount);

        for (var point = 1; point < pointCount; point++)
            waypoints.Add(straightPath[point].pos.ToVector3());

        path = new NavPath(waypoints, isComplete);
        return waypoints.Count > 0;
    }

    public bool TryGetNearestWalkablePoint(Vector3 position, out Vector3 point)
    {
        Query.FindNearestPoly(position.ToRecast(), NearestPolygonExtents, Filter, out var polygon, out var nearest, out _);

        point = polygon != 0 ? nearest.ToVector3() : position;
        return polygon != 0;
    }
}