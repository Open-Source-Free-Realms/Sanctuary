using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class CollectionDefinitionTests
{
    [TestMethod]
    public void CreateClientCollection_DerivesProgressFromOwnedItems()
    {
        var definition = new CollectionDefinition
        {
            Id = 17054,
            NameId = 17055,
            CategoryId = 10,
            Entries =
            [
                new CollectionEntryDefinition { Id = 41, ItemDefinitionId = 11081 },
                new CollectionEntryDefinition { Id = 42, ItemDefinitionId = 11082 }
            ]
        };
        IReadOnlySet<int> ownedItems = new HashSet<int> { 11082 };

        var clientCollection = definition.CreateClientCollection(123, ownedItems);

        Assert.IsTrue(definition.IsStarted(ownedItems));
        Assert.IsFalse(clientCollection.Entries[0].Collected);
        Assert.IsTrue(clientCollection.Entries[1].Collected);
        Assert.AreEqual(123ul, clientCollection.PlayerGuid);
    }

    [TestMethod]
    public void CreateClientCollectionEntry_UsesCollectionMetadata()
    {
        var definition = new CollectionDefinition
        {
            Id = 17054,
            NameId = 17055,
            CategoryId = 10
        };
        var entry = new CollectionEntryDefinition
        {
            Id = 41,
            NameId = 17056,
            IconId = 2124,
            IconTintId = 99
        };

        var clientEntry = definition.CreateClientCollectionEntry(entry, 2, true);

        Assert.AreEqual(entry.Id, clientEntry.Id);
        Assert.AreEqual(entry.Id, clientEntry.DefinitionId);
        Assert.AreEqual(3, clientEntry.Index);
        Assert.AreEqual(definition.Id, clientEntry.CollectionId);
        Assert.AreEqual(entry.NameId, clientEntry.NameId);
        Assert.AreEqual(entry.IconId, clientEntry.IconId);
        Assert.AreEqual(entry.IconTintId, clientEntry.IconTintId);
        Assert.IsTrue(clientEntry.Collected);
    }

    [TestMethod]
    public void CreateClientCollection_CombinesItemAndDirectEntryProgress()
    {
        var definition = new CollectionDefinition
        {
            Id = 288,
            RewardEntryType = RewardBundleEntryType.Item,
            RewardEntryItemGuid = 42,
            Entries =
            [
                new CollectionEntryDefinition { Id = 2038 },
                new CollectionEntryDefinition { Id = 2039, ItemDefinitionId = 11081 }
            ]
        };
        IReadOnlySet<int> ownedItems = new HashSet<int> { 11081 };
        IReadOnlySet<int> directEntries = new HashSet<int> { 2038 };

        var collection = definition.CreateClientCollection(123, ownedItems, directEntries);

        Assert.IsTrue(definition.IsComplete(ownedItems, directEntries));
        Assert.IsTrue(collection.Entries[0].Collected);
        Assert.IsTrue(collection.Entries[1].Collected);
        Assert.AreEqual((int)RewardBundleEntryType.Item, collection.Unknown13);
        Assert.AreEqual(42, collection.RewardEntryItemGuid);
    }

    [TestMethod]
    public void Load_RejectsEntryIdsSharedByDifferentCollections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"collections-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "[{\"Id\":1,\"NameId\":1,\"CategoryId\":1,\"Entries\":[{\"Id\":10}]}," +
            "{\"Id\":2,\"NameId\":2,\"CategoryId\":1,\"Entries\":[{\"Id\":10}]}]");

        try
        {
            var definitions = new CollectionDefinitionCollection(NullLogger.Instance);

            Assert.IsFalse(definitions.Load(path));
            Assert.AreEqual(0, definitions.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_RejectsItemIdsSharedByDifferentCollections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"collections-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "[{\"Id\":1,\"NameId\":1,\"CategoryId\":1,\"Entries\":[{\"Id\":10,\"ItemDefinitionId\":100}]}," +
            "{\"Id\":2,\"NameId\":2,\"CategoryId\":1,\"Entries\":[{\"Id\":20,\"ItemDefinitionId\":100}]}]");

        try
        {
            var definitions = new CollectionDefinitionCollection(NullLogger.Instance);

            Assert.IsFalse(definitions.Load(path));
            Assert.AreEqual(0, definitions.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
