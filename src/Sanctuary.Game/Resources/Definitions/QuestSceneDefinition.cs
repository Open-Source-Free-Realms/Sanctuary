using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class QuestSceneDefinition
{
    public int StartDelayMs { get; set; } = 2000;
    public int LineDelayMs { get; set; } = 10000;

    public List<QuestSceneActor> Actors { get; set; } = [];
    public List<QuestSceneLine> Lines { get; set; } = [];
}

public sealed class QuestSceneActor
{
    public int NpcId { get; set; }

    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public float Heading { get; set; }
}

public sealed class QuestSceneLine
{
    public int NpcId { get; set; }
    public int TextId { get; set; }

    public int? DelayMs { get; set; }
    public int? AnimationId { get; set; }
}
