using System;
using System.Numerics;

namespace Sanctuary.Core.Extensions;

public static class QuaternionExtensions
{
    public static Quaternion FromHeading(float heading) => new Vector3(MathF.Sin(heading), 0f, MathF.Cos(heading)).ToFlatRotation();

    public static Vector3 GetFacing(this Quaternion rotation)
    {
        return rotation.W == 0f
            ? new Vector3(rotation.X, 0f, rotation.Z)
            : Vector3.Transform(Vector3.UnitZ, rotation);
    }
}
