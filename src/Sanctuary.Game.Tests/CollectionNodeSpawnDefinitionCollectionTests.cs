using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class CollectionNodeSpawnDefinitionCollectionTests
{
    [TestMethod]
    [DoNotParallelize]
    public void Reload_DoesNotTemporarilyRemoveRetainedHardPoints()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sanctuary-collections-{Guid.NewGuid():N}");
        var zoneDirectory = Path.Combine(directory, "1");
        var path = Path.Combine(zoneDirectory, "briarwood-mushrooms.json");
        var previousContext = SynchronizationContext.Current;
        Directory.CreateDirectory(zoneDirectory);
        File.WriteAllText(path,
            "[{\"Id\":1,\"Position\":[1,2,3],\"Heading\":0}]");

        try
        {
            SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
            var collection = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(collection.Load(directory));

            var retainedHardPointWasMissing = false;
            collection.CollectionChanged += (_, _) =>
                retainedHardPointWasMissing |= !collection.ContainsKey(1);

            File.WriteAllText(path,
                "[{\"Id\":1,\"Position\":[4,5,6],\"Heading\":1}]");

            Assert.IsTrue(collection.Load(directory));
            Assert.IsFalse(retainedHardPointWasMissing);
            Assert.AreEqual(4f, collection[1].Position[0]);
            Assert.AreEqual("briarwood-mushrooms", collection[1].Pool);
            Assert.AreEqual(1, collection[1].ZoneDefinitionId);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void PersistentChanges_RoundTripThroughJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sanctuary-collections-{Guid.NewGuid():N}");
        var zoneDirectory = Path.Combine(directory, "1");
        var path = Path.Combine(zoneDirectory, "briarwood-mushrooms.json");
        Directory.CreateDirectory(zoneDirectory);
        File.WriteAllText(path, "[]");

        try
        {
            var collection = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(collection.Load(directory));
            Assert.IsTrue(collection.TryAddPersistent(
                "briarwood-mushrooms", 1, new Vector4(1, 2, 3, 1), 0.5f, out var added));

            var persistedJson = File.ReadAllText(path);
            Assert.IsFalse(persistedJson.Contains("\"Pool\"", StringComparison.Ordinal));

            var reloaded = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(reloaded.Load(directory));
            Assert.IsTrue(reloaded.TryGetValue(added.Id, out var persisted));
            Assert.AreEqual("briarwood-mushrooms", persisted.Pool);
            Assert.AreEqual(2f, persisted.Position[1]);
            Assert.AreEqual(1, persisted.ZoneDefinitionId);

            Assert.IsTrue(reloaded.TryRemovePersistent(added.Id));
            var empty = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(empty.Load(directory));
            Assert.AreEqual(0, empty.Count);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void PersistentChanges_OnlyRewriteTheTargetPoolFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sanctuary-collections-{Guid.NewGuid():N}");
        var firstZoneDirectory = Path.Combine(directory, "1");
        var secondZoneDirectory = Path.Combine(directory, "2");
        var targetPoolPath = Path.Combine(firstZoneDirectory, "target-pool.json");
        var siblingPoolPath = Path.Combine(firstZoneDirectory, "sibling-pool.json");
        var secondZonePath = Path.Combine(secondZoneDirectory, "second-zone-pool.json");
        const string siblingPoolJson = "[{\"Id\":2,\"Position\":[4,5,6],\"Heading\":1}]";
        const string secondZoneJson = "[{\"Id\":3,\"Position\":[7,8,9],\"Heading\":2}]";
        Directory.CreateDirectory(firstZoneDirectory);
        Directory.CreateDirectory(secondZoneDirectory);
        File.WriteAllText(targetPoolPath, "[{\"Id\":1,\"Position\":[1,2,3],\"Heading\":0}]");
        File.WriteAllText(siblingPoolPath, siblingPoolJson);
        File.WriteAllText(secondZonePath, secondZoneJson);

        try
        {
            var collection = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(collection.Load(directory));
            Assert.IsTrue(collection.TryAddPersistent(
                "target-pool", 1, new Vector4(10, 11, 12, 1), 0.5f, out _));

            Assert.AreEqual(siblingPoolJson, File.ReadAllText(siblingPoolPath));
            Assert.AreEqual(secondZoneJson, File.ReadAllText(secondZonePath));
            StringAssert.Contains(File.ReadAllText(targetPoolPath), "\"Id\": 4");
            Assert.HasCount(2, collection.Values.Where(spawn => spawn.Pool == "target-pool"));
            Assert.HasCount(3, collection.Values.Where(spawn => spawn.ZoneDefinitionId == 1));
            Assert.HasCount(1, collection.Values.Where(spawn => spawn.ZoneDefinitionId == 2));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Load_RejectsDuplicateIdsAcrossPoolFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sanctuary-collections-{Guid.NewGuid():N}");
        var zoneDirectory = Path.Combine(directory, "1");
        const string spawnJson = "[{\"Id\":1,\"Position\":[1,2,3],\"Heading\":0}]";
        Directory.CreateDirectory(zoneDirectory);
        File.WriteAllText(Path.Combine(zoneDirectory, "first-pool.json"), spawnJson);
        File.WriteAllText(Path.Combine(zoneDirectory, "second-pool.json"), spawnJson);

        try
        {
            var collection = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsFalse(collection.Load(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Load_RejectsNonCanonicalPoolFileName()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sanctuary-collections-{Guid.NewGuid():N}");
        var zoneDirectory = Path.Combine(directory, "1");
        Directory.CreateDirectory(zoneDirectory);
        File.WriteAllText(Path.Combine(zoneDirectory, "Uppercase-Pool.json"), "[]");

        try
        {
            var collection = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsFalse(collection.Load(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
            callback(state);
        }
    }
}
