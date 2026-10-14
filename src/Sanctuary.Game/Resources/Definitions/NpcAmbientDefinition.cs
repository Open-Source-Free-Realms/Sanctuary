using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class NpcAmbientDefinition
{
    public string ModelFileName { get; set; } = string.Empty;

    public List<int> AnimationIds { get; set; } = [];
    public int MinIntervalMs { get; set; }
    public int MaxIntervalMs { get; set; }

    public List<int> ConversationAnimationIds { get; set; } = [];
    public int ConversationMinGapMs { get; set; }
    public int ConversationMaxGapMs { get; set; }

    public List<int> IdleAnimationIds { get; set; } = [];
    public int IdleIntervalMs { get; set; }

    public float LookAtRadius { get; set; }
    public int LookAtIntervalMs { get; set; }
}
