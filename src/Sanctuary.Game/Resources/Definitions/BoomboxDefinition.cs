using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public class BoomboxDefinition
{
    public int ItemId { get; set; }
    public int ModelId { get; set; }
    public float Range { get; set; } = 15f;
    public int DurationMs { get; set; } = 180_000;
    public float SpawnOffset { get; set; } = 2f;
    public int SpawnAnimationId { get; set; } = 2100;
    public int SpawnEffectId { get; set; } = 21;
    public int[] EffectIds { get; set; } = [];
    public int[] DanceSequence { get; set; } = [];
    public int StandingDanceAnimationId { get; set; }
    public bool SynchronizedDances { get; set; }
    public int DanceBlendMs { get; set; }
    public Dictionary<int, int[]> DanceDurationsMs { get; set; } = [];
    public Dictionary<int, Dictionary<int, int>> IndependentDanceDurationsMs { get; set; } = [];
    public int Priority { get; set; }
    public int TransformReapplyDelayMs { get; set; }
    public int TransformModelId { get; set; }
}
