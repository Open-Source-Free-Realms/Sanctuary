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
            Id = 10,
            NameId = 17054,
            CategoryId = 3,
            Entries =
            [
                new CollectionEntryDefinition { Id = 41, ItemDefinitionId = 11081 },
                new CollectionEntryDefinition { Id = 42, ItemDefinitionId = 11082 }
            ]
        };
        IReadOnlySet<int> ownedItems = new HashSet<int> { 11082 };

        var clientCollection = definition.CreateClientCollection(123, ownedItems);

        Assert.IsTrue(definition.IsStarted(ownedItems));
        Assert.AreEqual(10, clientCollection.Id);
        Assert.AreEqual(17054, clientCollection.NameId);
        Assert.AreEqual(3, clientCollection.CategoryId);
        Assert.IsFalse(clientCollection.Entries[0].Collected);
        Assert.IsTrue(clientCollection.Entries[1].Collected);
        Assert.AreEqual(10, clientCollection.Entries[1].CollectionId);
        Assert.AreEqual(123ul, clientCollection.PlayerGuid);
    }
}
