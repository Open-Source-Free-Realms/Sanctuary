using System;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class CollectionResourceCatalogTests
{
    [TestMethod]
    public void CollectionResources_HaveValidCrossReferences()
    {
        var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));
        var collections = new CollectionDefinitionCollection(NullLogger.Instance);
        var coins = new CollectionCoinDefinitionCollection(NullLogger.Instance);
        var pools = new CollectionNodePoolDefinitionCollection(NullLogger.Instance);
        var types = new CollectionNodeTypeDefinitionCollection(NullLogger.Instance);
        var spawns = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
        var items = new ClientItemDefinitionCollection(NullLogger.Instance);
        var npcs = new NpcDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(collections.Load(Path.Combine(resources, "Collections.json")));
        Assert.IsTrue(coins.Load(Path.Combine(resources, "CollectionCoins.json")));
        Assert.IsTrue(pools.Load(Path.Combine(resources, "CollectionNodePools.json")));
        Assert.IsTrue(types.Load(Path.Combine(resources, "CollectionNodeTypes.json")));
        Assert.IsTrue(spawns.Load(Path.Combine(resources, "CollectionNodeSpawns")));
        Assert.IsTrue(items.Load(Path.Combine(resources, "ClientItemDefinitions.json")));
        Assert.IsTrue(npcs.Load(Path.Combine(resources, "Npcs.json")));

        var collectionItemDefinitionIds = collections.Values
            .SelectMany(collection => collection.Entries)
            .Where(entry => entry.ItemDefinitionId > 0)
            .Select(entry => entry.ItemDefinitionId)
            .ToHashSet();

        foreach (var collection in collections.Values)
        {
            Assert.IsFalse(collection.Entries.Any(entry => entry.ItemDefinitionId > 0 &&
                !items.ContainsKey(entry.ItemDefinitionId)), $"Collection {collection.Id} has an unknown item.");
        }

        foreach (var pool in pools.Values)
        {
            Assert.IsTrue(types.ContainsKey(pool.NodeType), $"Pool {pool.Key} has an unknown node type.");
            Assert.IsFalse(pool.DropTable.Any(drop => !items.ContainsKey(drop.ItemDefinitionId)),
                $"Pool {pool.Key} has an unknown drop item.");
            Assert.IsFalse(pool.DropTable.Any(drop => !collectionItemDefinitionIds.Contains(drop.ItemDefinitionId)),
                $"Pool {pool.Key} has a drop item that is not part of a collection.");
        }

        foreach (var spawn in spawns.Values)
        {
            Assert.IsTrue(pools.TryGetValue(spawn.Pool, out var pool),
                $"Spawn {spawn.Id} has an unknown pool.");
            Assert.AreEqual(pool.ZoneDefinitionId, spawn.ZoneDefinitionId,
                $"Spawn {spawn.Id} has a mismatched zone.");
        }

        foreach (var coin in coins.Values)
        {
            Assert.IsTrue(npcs.ContainsKey(coin.NpcDefinitionId),
                $"Coin {coin.NpcDefinitionId} has an unknown NPC.");
            Assert.IsTrue(collections.TryGetValue(coin.CollectionId, out var collection),
                $"Coin {coin.NpcDefinitionId} has an unknown collection.");
            var entry = collection.Entries.SingleOrDefault(entry => entry.Id == coin.EntryId);
            Assert.IsNotNull(entry,
                $"Coin {coin.NpcDefinitionId} has an unknown collection entry.");
            Assert.AreEqual(0, entry.ItemDefinitionId,
                $"Coin {coin.NpcDefinitionId} targets an inventory-backed collection entry.");
        }
    }
}
