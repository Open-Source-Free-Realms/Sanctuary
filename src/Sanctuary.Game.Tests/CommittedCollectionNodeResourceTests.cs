using System;
using System.IO;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class CommittedCollectionNodeResourceTests
{
    [TestMethod]
    public void BriarwoodResources_HaveValidatedPlayerFacingConfiguration()
    {
        var resourceDirectory = Path.Combine(AppContext.BaseDirectory, "Resources");
        var pools = new CollectionNodePoolDefinitionCollection(NullLogger.Instance);
        var spawns = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
        var types = new CollectionNodeTypeDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(pools.Load(Path.Combine(resourceDirectory, "CollectionNodePools.json")));
        Assert.IsTrue(spawns.Load(Path.Combine(resourceDirectory, "CollectionNodeSpawns.json")));
        Assert.IsTrue(types.Load(Path.Combine(resourceDirectory, "CollectionNodeTypes.json")));

        var commonPool = pools["briarwood-mushrooms"];
        var rarePool = pools["briarwood-mushrooms-rare"];
        Assert.AreEqual(12, commonPool.MaxActiveNodes);
        Assert.AreEqual(60, commonPool.RespawnSeconds);
        Assert.AreEqual(2, rarePool.MaxActiveNodes);
        Assert.AreEqual(300, rarePool.RespawnSeconds);

        var commonSpawns = spawns.Values.Where(spawn => spawn.Pool == commonPool.Key).ToArray();
        var rareSpawns = spawns.Values.Where(spawn => spawn.Pool == rarePool.Key).ToArray();
        Assert.AreEqual(66, commonSpawns.Length);
        Assert.AreEqual(10, rareSpawns.Length);

        var commonDrops = types[commonPool.NodeType].DropTable.Select(drop => drop.ItemDefinitionId).ToHashSet();
        var rareDrops = types[rarePool.NodeType].DropTable.Select(drop => drop.ItemDefinitionId).ToHashSet();
        Assert.AreEqual(6, commonDrops.Count);
        Assert.AreEqual(2, rareDrops.Count);
        Assert.AreEqual(0, commonDrops.Intersect(rareDrops).Count());
        Assert.AreEqual(8, commonDrops.Union(rareDrops).Count());

        var allSpawns = commonSpawns.Concat(rareSpawns).ToArray();
        for (var firstIndex = 0; firstIndex < allSpawns.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < allSpawns.Length; secondIndex++)
            {
                var first = new Vector3(allSpawns[firstIndex].Position);
                var second = new Vector3(allSpawns[secondIndex].Position);
                Assert.IsGreaterThan(10f, Vector3.Distance(first, second),
                    $"Hard points {allSpawns[firstIndex].Id} and {allSpawns[secondIndex].Id} are too close.");
            }
        }
    }
}
