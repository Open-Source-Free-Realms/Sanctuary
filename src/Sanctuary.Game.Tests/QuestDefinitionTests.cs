using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.QuestGoals;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class QuestDefinitionTests
{
    [TestMethod]
    public void Load_ReadsEachGoalAsTheSubclassItsTypeNames()
    {
        var quests = LoadQuests("""
            [
              {
                "QuestId": 1,
                "TargetNpcId": 500,
                "Goals": [
                  { "Type": 0, "NameId": 10 },
                  { "NameId": 11, "Type": 0, "Targets": [{ "NpcId": 600 }] },
                  { "NameId": 12, "ReachPosition": [1, 2, 3], "Type": 1 },
                  { "Type": 2, "NameId": 13, "CollectNodeType": "tin-ore", "RequiredCount": 5 }
                ]
              }
            ]
            """);

        Assert.IsTrue(quests.TryGet(1, out var quest));

        var defaulted = (TalkToNpcGoal)quest.Goals[0];
        CollectionAssert.AreEqual(new[] { 500 }, defaulted.AllTalkTargetNpcIds().ToArray(), "A talk goal with no NPC should resolve to the quest's TargetNpcId.");
        CollectionAssert.AreEqual(new[] { 600 }, ((TalkToNpcGoal)quest.Goals[1]).AllTalkTargetNpcIds().ToArray());
        Assert.AreEqual(new Vector3(1, 2, 3), ((ReachLocationGoal)quest.Goals[2]).ReachPosition);

        var collect = (CollectGoal)quest.Goals[3];
        Assert.AreEqual("tin-ore", collect.CollectNodeType);
        Assert.AreEqual(5, collect.RequiredCount);

        CollectionAssert.Contains(quests.ByTarget[500], 1);
        CollectionAssert.Contains(quests.ByTarget[600], 1);
    }

    [TestMethod]
    public void Load_RejectsAnUnknownGoalType()
    {
        var quests = new QuestDefinitionCollection(NullLogger.Instance);

        Assert.IsFalse(quests.Load(WriteTemp("""[ { "QuestId": 1, "Goals": [ { "Type": 99 } ] } ]""")));
    }

    [TestMethod]
    public void Load_RejectsAGoalWithoutAType()
    {
        var quests = new QuestDefinitionCollection(NullLogger.Instance);

        Assert.IsFalse(quests.Load(WriteTemp("""[ { "QuestId": 1, "Goals": [ { "NameId": 10 } ] } ]""")));
    }

    [TestMethod]
    public void Load_MakesExclusionsSymmetric()
    {
        var quests = LoadQuests("""
            [
              { "QuestId": 1, "TargetNpcId": 5, "ExcludesQuestIds": [2], "Goals": [ { "Type": 0, "NameId": 10 } ] },
              { "QuestId": 2, "TargetNpcId": 5, "Goals": [ { "Type": 0, "NameId": 20 } ] }
            ]
            """);

        Assert.IsTrue(quests.TryGet(2, out var second));
        CollectionAssert.Contains(second.ExcludesQuestIds, 1);
        Assert.IsFalse(second.IsOfferableFor(Log((1, false))));
    }

    [TestMethod]
    public void TurnInDialogueId_UsesTheLastLineOfTheFinalGoalsConversation()
    {
        var quests = LoadQuests("""
            [
              {
                "QuestId": 1,
                "TargetNpcId": 5,
                "Goals": [
                  { "Type": 0, "NameId": 10, "DialogueId": 100 },
                  { "Type": 0, "NameId": 11, "Dialogue": [ { "TextId": 200 }, { "TextId": 201 } ] }
                ]
              }
            ]
            """);

        Assert.IsTrue(quests.TryGet(1, out var quest));
        Assert.AreEqual(201, quest.TurnInDialogueId);
    }

    [TestMethod]
    public void IsOfferableFor_TreatsAMissingPrerequisiteAsNone()
    {
        var quest = new QuestDefinition { QuestId = 2 };

        Assert.IsTrue(quest.IsOfferableFor(Log()));

        quest.PrerequisiteQuestId = 1;

        Assert.IsFalse(quest.IsOfferableFor(Log()));
        Assert.IsFalse(quest.IsOfferableFor(Log((1, false))));
        Assert.IsTrue(quest.IsOfferableFor(Log((1, true))));
    }

    [TestMethod]
    public void IsOfferableFor_ExclusionWinsOverACompletedSharedPrerequisite()
    {
        var quest = new QuestDefinition { QuestId = 3, PrerequisiteQuestId = 1, ExcludesQuestIds = [2] };

        Assert.IsFalse(quest.IsOfferableFor(Log((1, true), (2, true))));
    }

    [TestMethod]
    public void QuestsJson_Loads()
    {
        var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));
        var quests = new QuestDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(quests.Load(Path.Combine(resources, "Quests.json")));
        Assert.IsTrue(quests.Quests.Count > 0);
        Assert.IsFalse(quests.Quests.Values.Any(quest => quest.PrerequisiteQuestId == 0 || quest.NextQuestId == 0),
            "Use null, not 0, for no prerequisite or next quest.");
        Assert.IsFalse(quests.Quests.Values.SelectMany(quest => quest.Goals).OfType<TalkToNpcGoal>()
            .Any(goal => !goal.AllTalkTargetNpcIds().Any()));
    }

    private static QuestDefinitionCollection LoadQuests(string json)
    {
        var quests = new QuestDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(quests.Load(WriteTemp(json)));

        return quests;
    }

    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static QuestLog Log(params (int QuestId, bool Completed)[] quests)
    {
        var log = new QuestLog();

        foreach (var (questId, completed) in quests)
        {
            if (completed)
                log.AddCompleted(questId);
            else
                log.Add(new QuestProgress(questId));
        }

        return log;
    }
}
