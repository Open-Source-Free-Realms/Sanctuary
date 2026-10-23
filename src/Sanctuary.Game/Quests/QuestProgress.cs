using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Quests;

public sealed class QuestProgress
{
    public QuestProgress(int questId)
    {
        QuestId = questId;
    }

    public int QuestId { get; }

    public bool Completed { get; set; }

    public int GoalIndex { get; private set; }

    public GoalProgress Goal { get; private set; } = new();

    public void StartGoal(QuestDefinition quest, int goalIndex)
    {
        GoalIndex = goalIndex;
        Goal = goalIndex < quest.Goals.Count ? quest.Goals[goalIndex].CreateProgress() : new GoalProgress();
    }

    public void Complete()
    {
        Completed = true;
        Goal = new GoalProgress();
    }
}
