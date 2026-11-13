using System;
using System.Numerics;

namespace Sanctuary.Game.Helpers;

public static class PositionHelper
{
    public static Vector4 GetPointNear(Vector4 target, float distance, Vector4? origin = null)
    {
        var angle = origin is { } source && (source.X != target.X || source.Z != target.Z)
            ? MathF.Atan2(source.X - target.X, source.Z - target.Z)
            : Random.Shared.NextSingle() * MathF.Tau;

        return new Vector4(target.X + MathF.Sin(angle) * distance, target.Y, target.Z + MathF.Cos(angle) * distance, target.W);
    }
}
