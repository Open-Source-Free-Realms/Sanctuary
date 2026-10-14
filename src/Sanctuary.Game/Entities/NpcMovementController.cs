using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Entities;

public sealed class NpcMovementController
{
    private const float WalkMaxStepUpHeight = 1f;
    private const float WalkMaxStepDownHeight = 3f;
    private const int WanderPickAttempts = 8;
    private const int RecentSpotCount = 3;
    private const float WanderMaxTurn = MathF.PI * 5f / 9f;
    private static readonly float[] FleeTurns = [0f, MathF.PI / 4f, -MathF.PI / 4f, MathF.PI / 2f, -MathF.PI / 2f, MathF.PI * 3f / 4f, -MathF.PI * 3f / 4f];
    private const float PatrolArrivedDistanceSquared = 0.25f;
    private const float WaypointTolerance = 0.25f;
    private const float StraightRouteMaxLengthRatio = 1.2f;

    private readonly Npc _npc;
    private readonly NpcMovementDefinition _definition;
    private readonly Vector3 _home;
    private readonly Queue<Vector3> _navMeshWaypoints = new();
    private readonly Queue<Vector3> _recentSpots = new();
    private readonly List<Vector3> _area;

    private long _nextMoveAt;
    private int _patrolIndex;
    private bool _fleeing;
    private float _wanderHeading = Random.Shared.NextSingle() * MathF.Tau;

    public NpcMovementController(Npc npc, NpcMovementDefinition definition)
    {
        _npc = npc;
        _definition = definition;
        _home = npc.Position.ToVector3();

        _area = definition.Area.ConvertAll(point => new Vector3(point[0], point[1], point[2]));

        _npc.Speed = definition.Speed;
        _npc.WaypointTolerance = WaypointTolerance;
        _npc.PositionUpdateIntervalMs = definition.UpdateIntervalMs;

        if (definition.Type == NpcMovementType.Patrol)
            _patrolIndex = GetNearestPatrolPointIndex();

        _nextMoveAt = Environment.TickCount64 + Random.Shared.Next(Math.Max(_definition.MaxPauseMs, _definition.PauseMs) + 1);
    }

    public void Update()
    {
        if (_fleeing && !_npc.IsMoving)
        {
            _fleeing = false;
            _npc.Speed = _definition.Speed;
        }

        if (!_fleeing && _definition.FleeDistance > 0f && TryStartFlee())
        {
            _nextMoveAt = Environment.TickCount64 + GetPauseMs();
            return;
        }

        if (_npc.IsMoving || Environment.TickCount64 < _nextMoveAt)
            return;

        var moved = _definition.Type switch
        {
            NpcMovementType.Wander => TryStartWanderLeg(),
            NpcMovementType.Patrol => TryStartPatrolLeg(),
            _ => false
        };

        _nextMoveAt = Environment.TickCount64 + (moved ? GetPauseMs() : _definition.MinPauseMs);
    }

    private bool TryStartWanderLeg()
    {
        var position = _npc.Position.ToVector3();

        if (position.DistanceSquaredXZ(_home) > _definition.Radius * _definition.Radius)
        {
            _npc.MoveTo(_home);
            return true;
        }

        for (var attempt = 0; attempt < WanderPickAttempts; attempt++)
        {
            var heading = attempt < WanderPickAttempts / 2
                ? _wanderHeading + (Random.Shared.NextSingle() * 2f - 1f) * WanderMaxTurn
                : Random.Shared.NextSingle() * MathF.Tau;
            var distance = _definition.MinLegDistance + Random.Shared.NextSingle() * (_definition.MaxLegDistance - _definition.MinLegDistance);
            var goal = position + new Vector3(MathF.Sin(heading), 0f, MathF.Cos(heading)) * distance;

            if (!IsInArea(goal) || IsNearRecentSpot(goal))
                continue;

            if (!TryMoveToReachableGoal(position, goal))
                continue;

            RememberSpot(position);
            _wanderHeading = heading;
            return true;
        }

        return false;
    }

    private bool TryStartFlee()
    {
        var position = _npc.Position.ToVector3();
        var fleeDistanceSquared = _definition.FleeDistance * _definition.FleeDistance;
        Vector3? nearestPlayerPosition = null;

        foreach (var (_, visiblePlayer) in _npc.VisiblePlayers)
        {
            var playerPosition = visiblePlayer.Position.ToVector3();
            var distanceSquared = position.DistanceSquaredXZ(playerPosition);

            if (distanceSquared > fleeDistanceSquared)
                continue;

            fleeDistanceSquared = distanceSquared;
            nearestPlayerPosition = playerPosition;
        }

        if (nearestPlayerPosition is not { } fromPlayer)
            return false;

        var away = new Vector3(position.X - fromPlayer.X, 0f, position.Z - fromPlayer.Z);
        away = away.LengthSquared() > 0f ? Vector3.Normalize(away) : Vector3.UnitZ;

        foreach (var turn in FleeTurns)
        {
            var goal = position + Vector3.Transform(away, Quaternion.CreateFromAxisAngle(Vector3.UnitY, turn)) * _definition.FleeLegDistance;

            if (!IsInArea(goal))
                continue;

            var speed = _npc.Speed;
            _npc.Speed = _definition.FleeSpeed;

            if (!TryMoveToReachableGoal(position, goal))
            {
                _npc.Speed = speed;
                continue;
            }

            _fleeing = true;
            return true;
        }

        return false;
    }

    private bool TryMoveToReachableGoal(Vector3 position, Vector3 goal)
    {
        if (_npc.Zone.HasNavMesh)
        {
            if (!_npc.Zone.TryFindReachableRoute(position, goal, _navMeshWaypoints, followRoads: false)
                || GetRouteLength(position) > StraightRouteMaxLengthRatio * MathF.Sqrt(position.DistanceSquaredXZ(goal)))
            {
                return false;
            }

            _npc.MoveTo(_npc.Zone.GetGroundPosition(goal, WalkMaxStepUpHeight, WalkMaxStepUpHeight + WalkMaxStepDownHeight), direct: true);
            return true;
        }

        if (!_npc.Zone.TryGetWalkableGoal(position, goal, WalkMaxStepUpHeight, WalkMaxStepDownHeight, out var walkableGoal))
            return false;

        _npc.MoveTo(walkableGoal, direct: true);
        return true;
    }

    private float GetRouteLength(Vector3 start)
    {
        var length = 0f;
        var previous = start;

        foreach (var waypoint in _navMeshWaypoints)
        {
            length += MathF.Sqrt(previous.DistanceSquaredXZ(waypoint));
            previous = waypoint;
        }

        return length;
    }

    private bool IsInArea(Vector3 goal) =>
        goal.DistanceSquaredXZ(_home) <= _definition.Radius * _definition.Radius
        && (_area.Count == 0 || new Vector4(goal, 1f).IsInPolygon(_area));

    private bool IsNearRecentSpot(Vector3 goal)
    {
        var minimumDistanceSquared = _definition.MinLegDistance * _definition.MinLegDistance;

        foreach (var recentSpot in _recentSpots)
        {
            if (goal.DistanceSquaredXZ(recentSpot) < minimumDistanceSquared)
                return true;
        }

        return false;
    }

    private void RememberSpot(Vector3 spot)
    {
        _recentSpots.Enqueue(spot);

        if (_recentSpots.Count > RecentSpotCount)
            _recentSpots.Dequeue();
    }

    private bool TryStartPatrolLeg()
    {
        if (_definition.Points.Count == 0)
            return false;

        var point = GetPatrolPoint(_patrolIndex);

        if (_npc.Position.ToVector3().DistanceSquaredXZ(point) <= PatrolArrivedDistanceSquared)
        {
            _patrolIndex = (_patrolIndex + 1) % _definition.Points.Count;
            point = GetPatrolPoint(_patrolIndex);
        }

        _npc.MoveTo(point);
        return true;
    }

    private int GetNearestPatrolPointIndex()
    {
        var position = _npc.Position.ToVector3();
        var nearestIndex = 0;
        var nearestDistanceSquared = float.MaxValue;

        for (var index = 0; index < _definition.Points.Count; index++)
        {
            var distanceSquared = position.DistanceSquaredXZ(GetPatrolPoint(index));

            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestIndex = index;
            }
        }

        return nearestIndex;
    }

    private Vector3 GetPatrolPoint(int index)
    {
        var point = _definition.Points[index];
        return new Vector3(point[0], point[1], point[2]);
    }

    private int GetPauseMs() => _definition.Type == NpcMovementType.Patrol
        ? _definition.PauseMs
        : Random.Shared.Next(_definition.MinPauseMs, Math.Max(_definition.MinPauseMs, _definition.MaxPauseMs) + 1)
            + Random.Shared.Next((_definition.MaxPauseMs - _definition.MinPauseMs) / 2 + 1);
}
