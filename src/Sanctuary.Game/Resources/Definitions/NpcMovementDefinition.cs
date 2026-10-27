using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public enum NpcMovementType
{
    Wander,
    Patrol
}

public sealed class NpcMovementDefinition
{
    public ulong Guid { get; set; }
    public string? ModelFileName { get; set; }
    public NpcMovementType Type { get; set; }

    public float Speed { get; set; }

    public int UpdateIntervalMs { get; set; }

    public float Radius { get; set; }
    public float MinLegDistance { get; set; }
    public float MaxLegDistance { get; set; }
    public int MinPauseMs { get; set; }
    public int MaxPauseMs { get; set; }

    public List<float[]> Area { get; set; } = [];

    public float FleeDistance { get; set; }
    public float FleeLegDistance { get; set; }
    public float FleeSpeed { get; set; }

    public int PauseMs { get; set; }
    public List<float[]> Points { get; set; } = [];
}
