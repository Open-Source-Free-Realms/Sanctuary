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
        return value.ToVector3().DistanceSquaredXZ(other) < radius * radius;
    }

    public static bool IsInRectangle(this Vector4 value, Vector3 p1, Vector3 p2)
    {
        var minX = Math.Min(p1.X, p2.X);
        var maxX = Math.Max(p1.X, p2.X);
        var minZ = Math.Min(p1.Z, p2.Z);
        var maxZ = Math.Max(p1.Z, p2.Z);

        return value.X > minX && value.X < maxX && value.Z > minZ && value.Z < maxZ;
    }

    public static bool IsInPolygon(this Vector4 value, IReadOnlyList<Vector3> points)
    {
        var inside = false;

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var previousPoint = points[(index + points.Count - 1) % points.Count];

            if ((point.Z > value.Z) != (previousPoint.Z > value.Z) &&
                value.X < ((double)previousPoint.X - point.X) * ((double)value.Z - point.Z) /
                ((double)previousPoint.Z - point.Z) + point.X)
                inside = !inside;
        }

        return inside;
    }

    public static Vector3 ToVector3(this Vector4 value)
    {
        return new Vector3(value.X, value.Y, value.Z);
    }
}
