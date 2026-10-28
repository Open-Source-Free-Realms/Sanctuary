using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Game.Pathing;

public sealed record NavPath(IReadOnlyList<Vector3> Waypoints, bool IsComplete);
