using System;
using System.Numerics;

namespace Sanctuary.Game.Helpers;

public static class FixtureHelper
{
    /// <summary>
    /// The NPC tilt (heading, pitch, roll) for a placed fixture's rotation, in the same heading
    /// convention as <see cref="Core.Extensions.QuaternionExtensions.FromHeading"/>.
    /// A tilted NPC is sent as a placed object (MovementType 0), which the client collides with,
    /// as it does for the zone's own placed objects.
    /// </summary>
    public static Vector4 GetTilt(Quaternion rotation)
    {
        // A flat facing direction (X/Z with W = 0, see ToFlatRotation) has no pitch or roll.
        if (rotation.W == 0f && rotation.Y == 0f)
            return new Vector4(MathF.Atan2(rotation.X, rotation.Z), 0f, 0f, 0f);

        rotation = Quaternion.Normalize(rotation);

        var forward = Vector3.Transform(Vector3.UnitZ, rotation);
        var up = Vector3.Transform(Vector3.UnitY, rotation);

        var heading = MathF.Atan2(forward.X, forward.Z);
        var pitch = MathF.Asin(Math.Clamp(-forward.Y, -1f, 1f));

        // Roll: how far "up" is turned around the forward axis, measured against the unrolled up.
        var flatRight = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward) is { } right && right.LengthSquared() > 1e-6f ? right : Vector3.UnitX);
        var unrolledUp = Vector3.Cross(forward, flatRight);
        var roll = MathF.Atan2(Vector3.Dot(up, flatRight), Vector3.Dot(up, unrolledUp));

        return new Vector4(heading, pitch, roll, 0f);
    }
}
