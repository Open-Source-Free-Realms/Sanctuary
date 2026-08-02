using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Game.Pathfinding;


public static class PathFollower
{
    public readonly record struct AdvanceResult(bool Moved, Vector3 NewPosition, Quaternion? NewRotation, bool Arrived);

    public static AdvanceResult Advance(PathState path, Vector3 currentPosition, float speed, float tolerance, float deltaSeconds)
    {

        if (!path.TryGetCurrentTarget(out var targetPosition))
            return new AdvanceResult(false, currentPosition, null, true);

        var toTarget = targetPosition - currentPosition;
        var distance = toTarget.Length();


        // This logic here is pretty simple: if we're within a tolerance of the waypoint,
        // 'path.Advance' will pop the waypoint. 
        if (distance <= tolerance)
        {
            path.Advance();
            return new AdvanceResult(false, currentPosition, null, path.IsEmpty);
        }

        var direction = Vector3.Normalize(toTarget);
        var step = direction * speed * deltaSeconds;
        if (step.Length() > distance)
            step = toTarget;

        var newPosition = currentPosition + step;

        // NOTE: FreeRealms conventions is y-up. Movement is typically mostly in the xy-plane
        // in my field, so just adding this here in case anyone else comes across a potentially
        // "unconventional" coordinate frame definition. 
        var newRotation = Quaternion.CreateFromYawPitchRoll(MathF.Atan2(direction.X, direction.Z), 0f, 0f);

        return new AdvanceResult(true, newPosition, newRotation, false);
    }
}