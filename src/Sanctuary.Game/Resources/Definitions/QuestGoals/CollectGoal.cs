using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class CollectGoal : QuestGoal
{
    // A CollectionNodeTypes.json key. Reuses the existing collection node system for pickups
    // instead of quests owning their own spawn points.
    public string CollectNodeType { get; set; } = string.Empty;

    public override bool Matches(QuestDefinition quest, QuestEvent questEvent)
        => RequiredCount > 0 && questEvent is NodeGatheredEvent { Node: var node } && node.TypeDefinition.Key == CollectNodeType;

    public override QuestGoalResult Progress(QuestDefinition quest, QuestEvent questEvent, GoalProgress progress)
    {
        if (!Matches(quest, questEvent))
            return QuestGoalResult.None;

        progress.Increment();

        return progress.Count >= RequiredCount ? QuestGoalResult.Completed : QuestGoalResult.Progressed;
    }
}
