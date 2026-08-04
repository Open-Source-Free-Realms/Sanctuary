using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Game.Pathfinding;

public interface IPathNode
{
    int Id { get; }
    Vector3 Position { get; }
    IReadOnlyList<(int NeighborId, float Distance)> Neighbors { get; }
}