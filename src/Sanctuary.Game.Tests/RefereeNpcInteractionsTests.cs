using System.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Interactions;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class RefereeNpcInteractionsTests
{
    [TestMethod]
    public void Interactions_HaveUniqueIds()
    {
        var provider = new RefereeNpcInteractions(NullLoggerFactory.Instance);

        var ids = provider.Interactions.Select(interaction => interaction.Id).ToList();

        Assert.AreEqual(ids.Count, ids.Distinct().Count());
    }

    [TestMethod]
    public void MainMenu_EveryEntryHasAHandler()
    {
        var provider = new RefereeNpcInteractions(NullLoggerFactory.Instance);

        var ids = provider.Interactions.Select(interaction => interaction.Id).ToHashSet();

        foreach (var entry in RefereeNpcInteractions.MainMenu.Concat(RefereeNpcInteractions.PropMenu))
            Assert.IsTrue(ids.Contains(entry.Id), $"Menu entry {entry.ButtonText} has no handler.");
    }
}
