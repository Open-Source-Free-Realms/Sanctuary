using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

using Sanctuary.Core.IO;

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

    [JsonConverter(typeof(Vector4JsonConverter))]
    public Vector4 Position { get; set; }

    public float Heading { get; set; }
}

public sealed class QuestSceneLine
{
    public int NpcId { get; set; }
    public int TextId { get; set; }

    public int? DelayMs { get; set; }
    public int? AnimationId { get; set; }
}
