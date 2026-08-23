using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.Actions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Pathfinding;

namespace Sanctuary.Game.Actions;

public sealed class MoveToAction : IAction
{
    private readonly Npc _npc;
    private readonly Vector3 _goal;
    private readonly bool _direct;
    private readonly PathState _path = new();

    public MoveToAction(Npc npc, Vector3 goal, bool direct = false)
    {
        _npc = npc;
        _goal = goal;
        _direct = direct;
    }

    public void OnStart()
    {
        if (_direct || _npc.Zone.Pathfinder is null)
        {
            var waypoints = new Queue<Vector3>();
            waypoints.Enqueue(_goal);
            _path.Set(waypoints);
            return;
        }

        var currentPosition = new Vector3(_npc.Position.X, _npc.Position.Y, _npc.Position.Z);
        var recomputedWaypoints = new PathBuilder(_npc.Zone.Pathfinder).TryRecompute(currentPosition, _goal);

        if (recomputedWaypoints is not null)
            _path.Set(recomputedWaypoints);
    }

    public bool OnTick()
    {
        var currentPosition = new Vector3(_npc.Position.X, _npc.Position.Y, _npc.Position.Z);
        var result = PathFollower.Advance(_path, currentPosition, _npc.Speed, _npc.WaypointTolerance, _npc.Zone.TickDeltaSeconds);

        if (result.Moved)
            _npc.UpdatePosition(new Vector4(result.NewPosition, 1f), result.NewRotation!.Value);

        return result.Arrived;
    }
}
