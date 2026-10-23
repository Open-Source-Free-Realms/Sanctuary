using System.Linq;
using System.Numerics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.QuestGoals;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class QuestGoalTests
{
    private static readonly QuestDefinition Quest = new() { QuestId = 1, TargetNpcId = 500 };

    [TestMethod]
    public void TalkGoal_CompletesAtItsTargetAndIgnoresOtherNpcs()
    {
        var goal = new TalkToNpcGoal { Targets = [new TalkTarget { NpcId = 600 }] };
        var progress = goal.CreateProgress();

        Assert.AreEqual(QuestGoalResult.None, goal.Progress(Quest, Talk(601), progress));
        Assert.AreEqual(QuestGoalResult.Completed, goal.Progress(Quest, Talk(600), progress));
    }

    [TestMethod]
    public void TalkGoal_WithoutTargetsFallsBackToTheQuestTarget()
    {
        var goal = new TalkToNpcGoal();

        Assert.AreEqual(QuestGoalResult.Completed, goal.Progress(Quest, Talk(500), goal.CreateProgress()));
    }

    [TestMethod]
    public void CountedTalkGoal_CountsEachNpcOnceUntilEnoughAreTalkedTo()
    {
        var goal = new TalkToNpcGoal
        {
            RequiredCount = 2,
            Targets = [new TalkTarget { NpcId = 600 }, new TalkTarget { NpcId = 601 }, new TalkTarget { NpcId = 602 }]
        };
        var progress = goal.CreateProgress();

        Assert.AreEqual(QuestGoalResult.Progressed, goal.Progress(Quest, Talk(600), progress));
        Assert.AreEqual(QuestGoalResult.None, goal.Progress(Quest, Talk(600), progress));
        Assert.AreEqual(1, progress.Count);
        Assert.AreEqual(QuestGoalResult.Completed, goal.Progress(Quest, Talk(602), progress));
    }

    [TestMethod]
    public void CountedTalkGoals_KeepSeparateProgressPerQuest()
    {
        var goal = new TalkToNpcGoal
        {
            RequiredCount = 2,
            Targets = [new TalkTarget { NpcId = 600 }, new TalkTarget { NpcId = 601 }]
        };
        var firstQuest = goal.CreateProgress();
        var secondQuest = goal.CreateProgress();

        goal.Progress(Quest, Talk(600), firstQuest);

        Assert.AreEqual(QuestGoalResult.Progressed, goal.Progress(Quest, Talk(600), secondQuest));
        Assert.AreEqual(1, firstQuest.Count);
    }

    [TestMethod]
    public void CollectGoal_CountsItsNodeTypeUntilTheRequiredCount()
    {
        var goal = new CollectGoal { CollectNodeType = "tin-ore", RequiredCount = 2 };
        var progress = goal.CreateProgress();

        Assert.AreEqual(QuestGoalResult.None, goal.Progress(Quest, Gather("iron-ore"), progress));
        Assert.AreEqual(QuestGoalResult.Progressed, goal.Progress(Quest, Gather("tin-ore"), progress));
        Assert.AreEqual(QuestGoalResult.Completed, goal.Progress(Quest, Gather("tin-ore"), progress));
    }

    [TestMethod]
    public void ReachGoal_CompletesOnlyInsideItsRadius()
    {
        var goal = new ReachLocationGoal { ReachPosition = new Vector3(100f, 0f, 100f), ReachRadius = 10f };
        var progress = goal.CreateProgress();

        Assert.AreEqual(QuestGoalResult.None, goal.Progress(Quest, new PlayerMovedEvent(new Vector4(120f, 0f, 100f, 1f)), progress));
        Assert.AreEqual(QuestGoalResult.Completed, goal.Progress(Quest, new PlayerMovedEvent(new Vector4(105f, 0f, 100f, 1f)), progress));
    }

    [TestMethod]
    public void GoalsIgnoreEventsMeantForOtherGoalTypes()
    {
        var talk = new TalkToNpcGoal { Targets = [new TalkTarget { NpcId = 600 }] };
        var collect = new CollectGoal { CollectNodeType = "tin-ore", RequiredCount = 1 };
        var reach = new ReachLocationGoal { ReachPosition = new Vector3(1f, 0f, 1f) };

        Assert.AreEqual(QuestGoalResult.None, talk.Progress(Quest, Gather("tin-ore"), talk.CreateProgress()));
        Assert.AreEqual(QuestGoalResult.None, collect.Progress(Quest, Talk(600), collect.CreateProgress()));
        Assert.AreEqual(QuestGoalResult.None, reach.Progress(Quest, Talk(500), reach.CreateProgress()));
    }

    [TestMethod]
    public void Progress_RoundTripsThroughItsSavedRows()
    {
        var talkGoal = new TalkToNpcGoal { RequiredCount = 3, Targets = [new TalkTarget { NpcId = 600 }, new TalkTarget { NpcId = 601 }] };
        var talk = talkGoal.CreateProgress();
        talkGoal.Progress(Quest, Talk(600), talk);
        talkGoal.Progress(Quest, Talk(601), talk);

        var loadedTalk = talkGoal.CreateProgress();
        foreach (var (targetId, count) in talk.Save())
            loadedTalk.Load(targetId, count);

        Assert.AreEqual(2, loadedTalk.Count);
        Assert.AreEqual(QuestGoalResult.None, talkGoal.Progress(Quest, Talk(600), loadedTalk));

        var collectGoal = new CollectGoal { CollectNodeType = "tin-ore", RequiredCount = 5 };
        var collect = collectGoal.CreateProgress();
        collectGoal.Progress(Quest, Gather("tin-ore"), collect);
        collectGoal.Progress(Quest, Gather("tin-ore"), collect);

        var loadedCollect = collectGoal.CreateProgress();
        foreach (var (targetId, count) in collect.Save())
            loadedCollect.Load(targetId, count);

        Assert.AreEqual(1, collect.Save().Count());
        Assert.AreEqual(2, loadedCollect.Count);
    }

    private static NpcTalkedEvent Talk(int npcId) => new(new Npc(null!) { DefinitionId = npcId });

    private static NodeGatheredEvent Gather(string nodeType) => new(new CollectionNode(null!,
        new CollectionNodeTypeDefinition { Key = nodeType, Name = nodeType },
        new CollectionNodePoolDefinition { Key = nodeType, NodeType = nodeType },
        new CollectionNodeSpawnDefinition()));
}
