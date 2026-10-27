using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class QuestLogTests
{
    [TestMethod]
    public void Complete_MovesTheQuestOutOfInProgress()
    {
        var log = new QuestLog();
        log.Add(new QuestProgress(1));
        log.Add(new QuestProgress(2));

        log.Complete(1);

        CollectionAssert.AreEqual(new[] { 2 }, log.InProgress.Select(quest => quest.QuestId).ToArray());
        Assert.IsTrue(log.IsCompleted(1));
        Assert.IsFalse(log.TryGetInProgress(1, out _));
        Assert.AreEqual(1, log.CompletedCount);
    }

    [TestMethod]
    public void Remove_DropsTheQuestWhetherOrNotItWasCompleted()
    {
        var log = new QuestLog();
        log.AddCompleted(1);
        log.Add(new QuestProgress(2));

        log.Remove(1);
        log.Remove(2);

        Assert.IsFalse(log.Contains(1));
        Assert.IsFalse(log.Contains(2));
        Assert.AreEqual(0, log.InProgress.Count());
        Assert.AreEqual(0, log.CompletedCount);
    }

    [TestMethod]
    public void Add_ReplacingACompletedQuestPutsItBackInProgress()
    {
        var log = new QuestLog();
        log.AddCompleted(1);

        log.Add(new QuestProgress(1));

        Assert.IsFalse(log.IsCompleted(1));
        Assert.IsTrue(log.TryGetInProgress(1, out _));
        Assert.AreEqual(1, log.All.Count);
        Assert.AreEqual(0, log.CompletedCount);
    }
}
