using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources.Definitions;

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
}
