using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Game.Pathfinding;

public sealed class MapNode : IPathNode
{
    public required int Id { get; init; }
    public required Vector3 Position { get; init; }
    public required IReadOnlyList<(int NeighborId, float Distance)> Neighbors { get; init; }
}
