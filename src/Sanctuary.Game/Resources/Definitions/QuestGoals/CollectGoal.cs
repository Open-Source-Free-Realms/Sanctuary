using Sanctuary.Game.Entities;
using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class CollectGoal : QuestGoal
{
    // A CollectionNodeTypes.json key. Reuses the existing collection node system for pickups
    // instead of quests owning their own spawn points.
    public string CollectNodeType { get; set; } = string.Empty;

    public override GoalProgress CreateProgress() => new CollectProgress();

    public bool CanGather(CollectionNode node, GoalProgress progress)
        => RequiredCount > 0
            && node.PoolDefinition.PlayerSpecific
            && node.TypeDefinition.Key == CollectNodeType
            && !(progress is CollectProgress collectProgress && collectProgress.HasGathered(node.SpawnDefinition.Id));

    public override bool Matches(QuestDefinition quest, QuestEvent questEvent)
        => RequiredCount > 0
            && questEvent is NodeGatheredEvent { Node: var node }
            && node.PoolDefinition.PlayerSpecific
            && node.TypeDefinition.Key == CollectNodeType;

    public override QuestGoalResult Progress(QuestDefinition quest, QuestEvent questEvent, GoalProgress progress)
    {
        if (!Matches(quest, questEvent) || questEvent is not NodeGatheredEvent { Node: var node })
            return QuestGoalResult.None;

        if (progress is not CollectProgress collectProgress || !collectProgress.TryAdd(node.SpawnDefinition.Id))
            return QuestGoalResult.None;

        return progress.Count >= RequiredCount ? QuestGoalResult.Completed : QuestGoalResult.Progressed;
    }
}
