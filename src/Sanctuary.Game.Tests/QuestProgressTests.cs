using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.QuestGoals;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class QuestProgressTests
{
    private static QuestDefinition ParallelQuest() => new()
    {
        QuestId = 1,
        TargetNpcId = 500,
        Goals =
        [
            new TalkToNpcGoal { NameId = 10, Targets = [new TalkTarget { NpcId = 600 }] },
            new CollectGoal { NameId = 20, RequiredCount = 2 },
            new CollectGoal { NameId = 30, RequiredCount = 3, WithPrevious = true },
            new TalkToNpcGoal { NameId = 40, Targets = [new TalkTarget { NpcId = 500 }] }
        ]
    };

    [TestMethod]
    public void StartStep_OpensEveryGoalInTheStep()
    {
        var quest = ParallelQuest();
        var progress = new QuestProgress(1);

        progress.StartStep(quest, 1);

        CollectionAssert.AreEquivalent(new[] { 1, 2 }, progress.OpenGoals.Keys.ToArray());
        Assert.AreEqual(3, progress.StepEnd);
        Assert.AreEqual(1, progress.GoalsDone);
    }

    [TestMethod]
    public void FinishGoal_OnlyAdvancesOnceTheWholeStepIsDone()
    {
        var quest = ParallelQuest();
        var progress = new QuestProgress(1);
        progress.StartStep(quest, 1);

        Assert.IsFalse(progress.FinishGoal(quest, 2));
        Assert.IsTrue(progress.IsGoalDone(2));
        Assert.IsFalse(progress.IsGoalDone(1));
        Assert.AreEqual(1, progress.FirstOpenGoalIndex);
        Assert.AreEqual(2, progress.GoalsDone);

        Assert.IsTrue(progress.FinishGoal(quest, 1));
        Assert.AreEqual(3, progress.GoalIndex);
        CollectionAssert.AreEqual(new[] { 3 }, progress.OpenGoals.Keys.ToArray());
    }

    [TestMethod]
    public void StepStartOf_FindsTheFirstGoalOfAParallelStep()
    {
        var quest = ParallelQuest();

        Assert.AreEqual(0, quest.StepStartOf(0));
        Assert.AreEqual(1, quest.StepStartOf(2));
        Assert.AreEqual(3, quest.StepStartOf(3));
        Assert.AreEqual(4, quest.StepEnd(4));
    }

    [TestMethod]
    public void FinishingTheLastStep_LeavesNothingOpen()
    {
        var quest = ParallelQuest();
        var progress = new QuestProgress(1);
        progress.StartStep(quest, 3);

        Assert.IsTrue(progress.FinishGoal(quest, 3));
        Assert.AreEqual(4, progress.GoalIndex);
        Assert.AreEqual(0, progress.OpenGoals.Count);
        Assert.AreEqual(4, progress.GoalsDone);
    }
}
