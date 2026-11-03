using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Quests;

public sealed class QuestProgress
{
    private readonly Dictionary<int, GoalProgress> _openGoals = [];

    public QuestProgress(int questId)
    {
        QuestId = questId;
    }

    public int QuestId { get; }

    public DateTimeOffset Started { get; init; } = DateTimeOffset.UtcNow;

    public bool Completed { get; private set; }

    public int GoalIndex { get; private set; }

    public int StepEnd { get; private set; }

    public IReadOnlyDictionary<int, GoalProgress> OpenGoals => _openGoals;

    public int FirstOpenGoalIndex => _openGoals.Count > 0 ? _openGoals.Keys.Min() : GoalIndex;

    public int GoalsDone => StepEnd - _openGoals.Count;

    public bool IsGoalDone(int goalIndex) => Completed || goalIndex < StepEnd && !_openGoals.ContainsKey(goalIndex);

    public GoalProgress ProgressFor(int goalIndex) => _openGoals.TryGetValue(goalIndex, out var progress) ? progress : new GoalProgress();

    public void StartStep(QuestDefinition quest, int stepStart)
    {
        GoalIndex = stepStart;
        StepEnd = quest.StepEnd(stepStart);
        _openGoals.Clear();

        for (var i = stepStart; i < StepEnd; i++)
            _openGoals[i] = quest.Goals[i].CreateProgress();
    }

    public bool FinishGoal(QuestDefinition quest, int goalIndex)
    {
        _openGoals.Remove(goalIndex);

        if (_openGoals.Count > 0)
            return false;

        StartStep(quest, StepEnd);
        return true;
    }

    internal void Complete()
    {
        Completed = true;
        _openGoals.Clear();
    }
}
