using System;
using System.Numerics;

namespace Sanctuary.Game.Pathing;

public sealed record ChaseSettings(float MinDistance, float MaxDistance, float SpreadAngleRadians, float StopTolerance, float ReplanFrequency)
{
    public Vector3 GetOffset(Vector3 chaserPosition, Vector3 targetPosition)
    {
        var approachAngle = MathF.Atan2(chaserPosition.X - targetPosition.X, chaserPosition.Z - targetPosition.Z);
        var offsetAngle = approachAngle + (Random.Shared.NextSingle() * 2f - 1f) * SpreadAngleRadians;
        var distance = MinDistance + Random.Shared.NextSingle() * (MaxDistance - MinDistance);

        return new Vector3(MathF.Sin(offsetAngle), 0f, MathF.Cos(offsetAngle)) * distance;
    }
}