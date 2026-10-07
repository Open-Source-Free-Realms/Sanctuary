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
    // Native group repeated by the client's standing-animation controller.
    public int StandingDanceAnimationId { get; set; }
    public bool SynchronizedDances { get; set; }
    // Overlap the client's native emote ease-out/ease-in window between clips.
    public int DanceBlendMs { get; set; }
    // For styles without a native group: clip durations, by player model, in sequence order.
    public Dictionary<int, int[]> DanceDurationsMs { get; set; } = [];
    // Concrete clips for independent random playback, indexed by model then animation ID.
    public Dictionary<int, Dictionary<int, int>> IndependentDanceDurationsMs { get; set; } = [];
    public int Priority { get; set; }
    public int TransformReapplyDelayMs { get; set; }
    public int TransformModelId { get; set; }
}
