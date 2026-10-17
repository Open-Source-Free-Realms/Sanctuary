namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class CollectGoal : QuestGoal
{
    // A CollectionNodeTypes.json key. Reuses the existing collection node system for pickups
    // instead of quests owning their own spawn points.
    public string CollectNodeType { get; set; } = string.Empty;

    public int RequiredCount { get; set; }
}
