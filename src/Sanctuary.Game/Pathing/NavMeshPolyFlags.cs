using System;

namespace Sanctuary.Game.Pathing;

[Flags]
public enum NavMeshPolyFlags
{
    Walk = 1,
    Swim = 2,
    Outside = 4
}
