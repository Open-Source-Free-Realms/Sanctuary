using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;

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
                "TargetGuid": 500,
                "Goals": [
                  { "NameId": 10 },
                  { "NameId": 11, "TargetGuid": 600 },
                  { "NameId": 12, "ReachPosition": [1, 2, 3], "Type": 1 },
                  { "Type": "Collect", "NameId": 13, "CollectNodeType": "tin-ore", "RequiredCount": 5 }
                ]
              }
            ]
            """);

        Assert.IsTrue(quests.TryGet(1, out var quest));

        var defaulted = (TalkToNpcGoal)quest.Goals[0];
        Assert.AreEqual(500UL, defaulted.TargetGuid, "A talk goal with no NPC should resolve to the quest's TargetGuid.");
        Assert.AreEqual(600UL, ((TalkToNpcGoal)quest.Goals[1]).TargetGuid);
        Assert.AreEqual(3, ((ReachLocationGoal)quest.Goals[2]).ReachPosition.Length);

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
    public void IsOfferableFor_TreatsAMissingPrerequisiteAsNone()
    {
        var quest = new QuestDefinition { QuestId = 2 };

        Assert.IsTrue(quest.IsOfferableFor(new Dictionary<int, bool>()));

        quest.PrerequisiteQuestId = 1;

        Assert.IsFalse(quest.IsOfferableFor(new Dictionary<int, bool>()));
        Assert.IsFalse(quest.IsOfferableFor(new Dictionary<int, bool> { [1] = false }));
        Assert.IsTrue(quest.IsOfferableFor(new Dictionary<int, bool> { [1] = true }));
    }

    [TestMethod]
    public void IsOfferableFor_ExclusionWinsOverACompletedSharedPrerequisite()
    {
        var quest = new QuestDefinition { QuestId = 3, PrerequisiteQuestId = 1, ExcludesQuestIds = [2] };

        Assert.IsFalse(quest.IsOfferableFor(new Dictionary<int, bool> { [1] = true, [2] = true }));
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
            .Any(goal => !goal.AllTalkTargetGuids().Any()));
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
}
