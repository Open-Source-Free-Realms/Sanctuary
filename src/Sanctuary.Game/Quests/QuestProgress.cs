using System;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Quests;

public sealed class QuestProgress
{
    public QuestProgress(int questId)
    {
        QuestId = questId;
    }

    public int QuestId { get; }

    public DateTimeOffset Started { get; init; } = DateTimeOffset.UtcNow;

    public bool Completed { get; private set; }

    public int GoalIndex { get; private set; }

    public GoalProgress Goal { get; private set; } = new();

    public void StartGoal(QuestDefinition quest, int goalIndex)
    {
        GoalIndex = goalIndex;
        Goal = goalIndex < quest.Goals.Count ? quest.Goals[goalIndex].CreateProgress() : new GoalProgress();
    }

    public void Advance(QuestDefinition quest) => StartGoal(quest, GoalIndex + 1);

    internal void Complete()
    {
        Completed = true;
        Goal = new GoalProgress();
    }
}
