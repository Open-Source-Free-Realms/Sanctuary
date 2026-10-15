using System;
using System.Numerics;

using Sanctuary.Core.Extensions;

namespace Sanctuary.Game.Pathfinding;


public static class PathFollower
{
    public readonly record struct AdvanceResult(bool Moved, Vector3 NewPosition, Quaternion? NewRotation, bool Arrived);

    public static AdvanceResult Advance(PathState path, Vector3 currentPosition, float speed, float tolerance, float deltaSeconds)
    {
        Vector3 targetPosition;
        Vector3 toTarget;
        float distance;

        while (true)
        {
            if (!path.TryGetCurrentTarget(out targetPosition))
                return new AdvanceResult(false, currentPosition, null, true);

            toTarget = targetPosition - currentPosition;
            distance = toTarget.Length();

            if (toTarget.X * toTarget.X + toTarget.Z * toTarget.Z > tolerance * tolerance)
                break;

            path.Advance();
        }

        var direction = toTarget / distance;
        var stepLength = speed * deltaSeconds;
        var newPosition = stepLength >= distance ? targetPosition : currentPosition + direction * stepLength;

        return new AdvanceResult(true, newPosition, direction.ToFlatRotation(), false);
    }
}
