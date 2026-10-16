using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Game.Pathfinding;


public sealed class PathState
{
    private readonly object _lock = new();
    private Queue<Vector3> _waypoints = new();


    public void Set(Queue<Vector3> waypoints)
    {
        lock (_lock)
            _waypoints = waypoints;
    }


    public void SetGoal(Vector3 goalPosition)
    {
        lock (_lock)
        {
            var waypoints = new Queue<Vector3>(_waypoints.Count);
            var keepCount = _waypoints.Count - 1;

            foreach (var waypoint in _waypoints)
            {
                if (keepCount-- <= 0)
                    break;

                waypoints.Enqueue(waypoint);
            }

            waypoints.Enqueue(goalPosition);
            _waypoints = waypoints;
        }
    }

    public void SetTarget(Vector3 targetPosition)
    {
        lock (_lock)
        {
            _waypoints.Clear();
            _waypoints.Enqueue(targetPosition);
        }
    }

    public void Clear()
    {
        lock (_lock)
            _waypoints.Clear();
    }

    public bool TryGetCurrentTarget(out Vector3 targetPosition)
    {
        lock (_lock)
        {
            if (_waypoints.Count == 0)
            {
                targetPosition = default;
                return false;
            }

            targetPosition = _waypoints.Peek();
            return true;
        }
    }

    public void Advance()
    {
        lock (_lock)
        {
            if (_waypoints.Count > 0)
                _waypoints.Dequeue();
        }
    }

    public bool IsEmpty
    {
        get { lock (_lock) return _waypoints.Count == 0; }
    }
}