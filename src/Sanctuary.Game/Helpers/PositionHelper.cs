using System;
using System.Numerics;

namespace Sanctuary.Game.Helpers;

public static class PositionHelper
{
    public static Vector4 GetPointNear(Vector4 target, float distance, Vector4? from = null)
    {
        var angle = from is { } origin && (origin.X != target.X || origin.Z != target.Z)
            ? MathF.Atan2(origin.X - target.X, origin.Z - target.Z)
            : Random.Shared.NextSingle() * MathF.Tau;

        return new Vector4(target.X + MathF.Sin(angle) * distance, target.Y, target.Z + MathF.Cos(angle) * distance, target.W);
    }
}
