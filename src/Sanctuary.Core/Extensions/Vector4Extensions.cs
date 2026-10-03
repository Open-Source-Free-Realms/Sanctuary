using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Core.Extensions;

public static class Vector4Extensions
{
    public static bool IsInRange(this Vector4 value, Vector4 other, float range)
    {
        var distance = value - other;

        return distance.LengthSquared() <= range * range;
    }

    public static bool IsInCircle(this Vector4 value, Vector3 other, float radius)
    {
        return Math.Pow(value.X - other.X, 2) + Math.Pow(value.Z - other.Z, 2) < Math.Pow(radius, 2);
    }

    public static bool IsInRectangle(this Vector4 value, Vector3 p1, Vector3 p2)
    {
        var minX = Math.Min(p1.X, p2.X);
        var maxX = Math.Max(p1.X, p2.X);
        var minZ = Math.Min(p1.Z, p2.Z);
        var maxZ = Math.Max(p1.Z, p2.Z);

        return value.X > minX && value.X < maxX && value.Z > minZ && value.Z < maxZ;
    }

    public static bool IsInArea(this Vector4 value, IReadOnlyList<float[]> points)
    {
        var inside = false;

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var previousPoint = points[(index + points.Count - 1) % points.Count];

            if ((point[1] > value.Z) != (previousPoint[1] > value.Z) &&
                value.X < ((double)previousPoint[0] - point[0]) * ((double)value.Z - point[1]) /
                ((double)previousPoint[1] - point[1]) + point[0])
                inside = !inside;
        }

        return inside;
    }
}
