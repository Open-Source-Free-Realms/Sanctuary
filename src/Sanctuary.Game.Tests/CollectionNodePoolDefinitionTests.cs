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
public sealed class CollectionNodePoolDefinitionTests
{
    [TestMethod]
    public void GetTargetActiveCount_UsesEveryHardPointWhenUncapped()
    {
        var pool = CreatePool(maxActiveNodes: 0);

        Assert.AreEqual(12, pool.GetTargetActiveCount(12));
    }

    [TestMethod]
    public void GetTargetActiveCount_RespectsConfiguredCap()
    {
        var pool = CreatePool(maxActiveNodes: 3);

        Assert.AreEqual(3, pool.GetTargetActiveCount(12));
        Assert.AreEqual(2, pool.GetTargetActiveCount(2));
    }

    [TestMethod]
    public void Load_RejectsNegativeActiveNodeCap()
    {
        var path = Path.Combine(Path.GetTempPath(), $"collection-node-pools-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "[{\"Key\":\"test\",\"ZoneDefinitionId\":1,\"NodeType\":\"mushrooms\"," +
            "\"MaxActiveNodes\":-1,\"RespawnSeconds\":30}]");

        try
        {
            var definitions = new CollectionNodePoolDefinitionCollection(NullLogger.Instance);

            Assert.IsFalse(definitions.Load(path));
            Assert.AreEqual(0, definitions.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void TryUpdatePersistent_WritesSettingsThatSurviveReload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"collection-node-pools-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "[{\"Key\":\"test\",\"ZoneDefinitionId\":1,\"NodeType\":\"mushrooms\"," +
            "\"MaxActiveNodes\":0,\"RespawnSeconds\":30}]");

        try
        {
            var definitions = new CollectionNodePoolDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(definitions.Load(path));

            Assert.IsTrue(definitions.TryUpdatePersistent("test", maxActiveNodes: 2, respawnSeconds: 5));

            var reloaded = new CollectionNodePoolDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(reloaded.Load(path));
            Assert.IsTrue(reloaded.TryGetValue("test", out var pool));
            Assert.AreEqual(2, pool.MaxActiveNodes);
            Assert.AreEqual(5, pool.RespawnSeconds);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [TestMethod]
    public void Load_KeepsCommonAndRarePoolsIndependentlyConfigured()
    {
        var path = Path.Combine(Path.GetTempPath(), $"collection-node-pools-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "[{\"Key\":\"briarwood-mushrooms\",\"ZoneDefinitionId\":1," +
            "\"NodeType\":\"mushrooms\",\"MaxActiveNodes\":12,\"RespawnSeconds\":60}," +
            "{\"Key\":\"briarwood-mushrooms-rare\",\"ZoneDefinitionId\":1," +
            "\"NodeType\":\"rare-mushrooms\",\"MaxActiveNodes\":2,\"RespawnSeconds\":300}]");

        try
        {
            var definitions = new CollectionNodePoolDefinitionCollection(NullLogger.Instance);

            Assert.IsTrue(definitions.Load(path));
            Assert.AreEqual(2, definitions.Count);
            Assert.AreEqual("mushrooms", definitions["briarwood-mushrooms"].NodeType);
            Assert.AreEqual(12, definitions["briarwood-mushrooms"].MaxActiveNodes);
            Assert.AreEqual(60, definitions["briarwood-mushrooms"].RespawnSeconds);
            Assert.AreEqual("rare-mushrooms", definitions["briarwood-mushrooms-rare"].NodeType);
            Assert.AreEqual(2, definitions["briarwood-mushrooms-rare"].MaxActiveNodes);
            Assert.AreEqual(300, definitions["briarwood-mushrooms-rare"].RespawnSeconds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void SelectSpawnsToActivate_ExcludesActivePointsAndFillsCapWithoutDuplicates()
    {
        var pool = CreatePool(maxActiveNodes: 3);
        var hardPoints = Enumerable.Range(1, 6)
            .Select(id => new CollectionNodeSpawnDefinition
            {
                Id = id,
                Pool = pool.Key
            })
            .ToArray();
        IReadOnlySet<int> activeIds = new HashSet<int> { 2 };

        var selected = pool.SelectSpawnsToActivate(hardPoints, activeIds, int.MaxValue);

        Assert.AreEqual(2, selected.Count);
        Assert.IsFalse(selected.Any(spawn => activeIds.Contains(spawn.Id)));
        Assert.AreEqual(selected.Count, selected.Select(spawn => spawn.Id).Distinct().Count());
    }

    [TestMethod]
    public void SelectSpawnsToActivate_RespectsPerRefillLimit()
    {
        var pool = CreatePool(maxActiveNodes: 3);
        var hardPoints = Enumerable.Range(1, 6)
            .Select(id => new CollectionNodeSpawnDefinition
            {
                Id = id,
                Pool = pool.Key
            });

        var selected = pool.SelectSpawnsToActivate(
            hardPoints, new HashSet<int>(), maximumToActivate: 1);

        Assert.AreEqual(1, selected.Count);
    }

    [TestMethod]
    public void SelectSpawnsToActivate_AvoidsJustCollectedPointWhenAlternativeExists()
    {
        var pool = CreatePool(maxActiveNodes: 1);
        var hardPoints = Enumerable.Range(1, 3)
            .Select(id => new CollectionNodeSpawnDefinition
            {
                Id = id,
                Pool = pool.Key
            });

        var selected = pool.SelectSpawnsToActivate(
            hardPoints, new HashSet<int>(), maximumToActivate: 1, avoidHardPointId: 2);

        Assert.AreEqual(1, selected.Count);
        Assert.AreNotEqual(2, selected[0].Id);
    }

    [TestMethod]
    public void SelectSpawnsToActivate_ReusesJustCollectedPointWhenItIsOnlyOption()
    {
        var pool = CreatePool(maxActiveNodes: 1);
        var hardPoints = new[]
        {
            new CollectionNodeSpawnDefinition
            {
                Id = 2,
                Pool = pool.Key
            }
        };

        var selected = pool.SelectSpawnsToActivate(
            hardPoints, new HashSet<int>(), maximumToActivate: 1, avoidHardPointId: 2);

        Assert.AreEqual(1, selected.Count);
        Assert.AreEqual(2, selected[0].Id);
    }

    private static CollectionNodePoolDefinition CreatePool(int maxActiveNodes)
    {
        return new CollectionNodePoolDefinition
        {
            Key = "briarwood-mushrooms",
            NodeType = "mushrooms",
            MaxActiveNodes = maxActiveNodes
        };
    }
}
