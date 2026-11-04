using System.Numerics;

using DotRecast.Core.Numerics;

namespace Sanctuary.Game.Pathing;

internal static class RecastExtensions
{
    public static RcVec3f ToRecast(this Vector3 vector) => new(vector.X, vector.Y, vector.Z);

    public static Vector3 ToVector3(this RcVec3f vector) => new(vector.X, vector.Y, vector.Z);
}
