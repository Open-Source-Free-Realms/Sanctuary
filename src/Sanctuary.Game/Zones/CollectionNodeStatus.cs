using System.Numerics;

namespace Sanctuary.Game.Zones;

public sealed record CollectionNodePoolStatus(
    string Key,
    string NodeType,
    int ActiveCount,
    int HardPointCount,
    int TargetActiveCount,
    int RespawnSeconds);

public sealed record CollectionNodeSpawnStatus(
    int Id,
    string Pool,
    Vector4 Position,
    bool Active);
