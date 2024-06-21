using System;
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
}