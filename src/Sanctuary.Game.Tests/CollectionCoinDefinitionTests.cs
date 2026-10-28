using System;
using System.IO;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class CollectionCoinDefinitionTests
{
    [TestMethod]
    public void Load_RejectsDuplicateCollectionEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"collection-coins-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "[{\"NpcDefinitionId\":100,\"CollectionId\":10,\"EntryId\":20}," +
            "{\"NpcDefinitionId\":101,\"CollectionId\":10,\"EntryId\":20}]");

        try
        {
            var definitions = new CollectionCoinDefinitionCollection(NullLogger.Instance);

            Assert.IsFalse(definitions.Load(path));
            Assert.AreEqual(0, definitions.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void IsVisibleTo_HidesCoinOnlyFromPlayerWhoCollectedIt()
    {
        var definition = new CollectionCoinDefinition
        {
            NpcDefinitionId = 100,
            CollectionId = 10,
            EntryId = 20
        };
        using var zone = TestZone.Create();

        var coin = new CollectionCoin(zone, definition);
        var collectedPlayer = new Player(zone, null!, null!, null!);
        var otherPlayer = new Player(zone, null!, null!, null!);
        collectedPlayer.CollectedCollectionEntryIds.TryAdd(definition.EntryId);

        Assert.IsFalse(coin.IsVisibleTo(collectedPlayer));
        Assert.IsTrue(coin.IsVisibleTo(otherPlayer));
    }
}
