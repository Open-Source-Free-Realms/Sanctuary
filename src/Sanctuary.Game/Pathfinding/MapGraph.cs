using System.Collections.Generic;

namespace Sanctuary.Game.Pathfinding;

public sealed class MapGraph
{
    public required IReadOnlyDictionary<int, MapNode> Nodes { get; init; }
}