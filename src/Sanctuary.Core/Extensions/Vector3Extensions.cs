using System.Numerics;

namespace Sanctuary.Core.Extensions;

public static class Vector3Extensions
{
    private const float PositionW = 1f;

    public static Vector4 ToVector4(this Vector3 value)
    {
        return new Vector4(value, PositionW);
    }

    public static float DistanceSquaredXZ(this Vector3 value, Vector3 other)
    {
        var deltaX = value.X - other.X;
        var deltaZ = value.Z - other.Z;

        return deltaX * deltaX + deltaZ * deltaZ;
    }

    public static Quaternion ToFlatRotation(this Vector3 direction)
    {
        var flatDirection = new Vector2(direction.X, direction.Z);
        flatDirection = flatDirection.LengthSquared() > 0f ? Vector2.Normalize(flatDirection) : Vector2.UnitY;

        return new Quaternion(flatDirection.X, 0f, flatDirection.Y, 0f);
    }
}
