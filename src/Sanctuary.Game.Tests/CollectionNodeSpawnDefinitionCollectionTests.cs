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
        var path = Path.Combine(directory, "1.json");
        var previousContext = SynchronizationContext.Current;
        Directory.CreateDirectory(directory);
        File.WriteAllText(path,
            "[{\"Id\":1,\"Pool\":\"briarwood-mushrooms\",\"Position\":[1,2,3],\"Heading\":0}]");

        try
        {
            SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
            var collection = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(collection.Load(directory));

            var retainedHardPointWasMissing = false;
            collection.CollectionChanged += (_, _) =>
                retainedHardPointWasMissing |= !collection.ContainsKey(1);

            File.WriteAllText(path,
                "[{\"Id\":1,\"Pool\":\"briarwood-mushrooms\",\"Position\":[4,5,6],\"Heading\":1}]");

            Assert.IsTrue(collection.Load(directory));
            Assert.IsFalse(retainedHardPointWasMissing);
            Assert.AreEqual(4f, collection[1].Position[0]);
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
        var path = Path.Combine(directory, "1.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "[]");

        try
        {
            var collection = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(collection.Load(directory));
            Assert.IsTrue(collection.TryAddPersistent(
                "briarwood-mushrooms", 1, new Vector4(1, 2, 3, 1), 0.5f, out var added));

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
    public void PersistentChanges_OnlyRewriteTheTargetZoneFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sanctuary-collections-{Guid.NewGuid():N}");
        var firstZonePath = Path.Combine(directory, "1.json");
        var secondZonePath = Path.Combine(directory, "2.json");
        const string secondZoneJson =
            "[{\"Id\":2,\"Pool\":\"second-zone-pool\",\"Position\":[4,5,6],\"Heading\":1}]";
        Directory.CreateDirectory(directory);
        File.WriteAllText(firstZonePath,
            "[{\"Id\":1,\"Pool\":\"first-zone-pool\",\"Position\":[1,2,3],\"Heading\":0}]");
        File.WriteAllText(secondZonePath, secondZoneJson);

        try
        {
            var collection = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(collection.Load(directory));
            Assert.IsTrue(collection.TryAddPersistent(
                "first-zone-pool", 1, new Vector4(7, 8, 9, 1), 0.5f, out _));

            Assert.AreEqual(secondZoneJson, File.ReadAllText(secondZonePath));
            Assert.HasCount(2, collection.Values.Where(spawn => spawn.ZoneDefinitionId == 1));
            Assert.HasCount(1, collection.Values.Where(spawn => spawn.ZoneDefinitionId == 2));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Load_RejectsDuplicateIdsAcrossZoneFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sanctuary-collections-{Guid.NewGuid():N}");
        const string spawnJson =
            "[{\"Id\":1,\"Pool\":\"test-pool\",\"Position\":[1,2,3],\"Heading\":0}]";
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "1.json"), spawnJson);
        File.WriteAllText(Path.Combine(directory, "2.json"), spawnJson);

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
