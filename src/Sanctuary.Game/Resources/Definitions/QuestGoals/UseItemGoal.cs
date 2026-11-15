using System.Numerics;
using System.Text.Json.Serialization;

using Sanctuary.Core.IO;
using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class UseItemGoal : QuestGoal
{
    public int ItemDefinitionId { get; set; }

    [JsonConverter(typeof(Vector3XzJsonConverter))]
    public Vector3 TargetPosition { get; set; }

    public int ZoneDefinitionId { get; set; } = ReachLocationGoal.FabledRealmsZoneDefinitionId;

    public override bool Matches(QuestDefinition quest, QuestEvent questEvent)
        => questEvent is ItemUsedEvent { ItemDefinitionId: var itemDefinitionId }
            && itemDefinitionId == ItemDefinitionId;

    public override QuestGoalResult Progress(QuestDefinition quest, QuestEvent questEvent, GoalProgress progress)
    {
        if (!Matches(quest, questEvent))
            return QuestGoalResult.None;

        progress.Increment();

        return progress.Count >= RequiredCount ? QuestGoalResult.Completed : QuestGoalResult.Progressed;
    }
}
