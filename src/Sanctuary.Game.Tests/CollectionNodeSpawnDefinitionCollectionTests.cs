using System;
using System.IO;
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
        var path = Path.Combine(directory, "CollectionNodeSpawns.json");
        var previousContext = SynchronizationContext.Current;
        Directory.CreateDirectory(directory);
        File.WriteAllText(path,
            "[{\"Id\":1,\"Pool\":\"briarwood-mushrooms\",\"Position\":[1,2,3],\"Heading\":0}]");

        try
        {
            SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
            var collection = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(collection.Load(path));

            var retainedHardPointWasMissing = false;
            collection.CollectionChanged += (_, _) =>
                retainedHardPointWasMissing |= !collection.ContainsKey(1);

            File.WriteAllText(path,
                "[{\"Id\":1,\"Pool\":\"briarwood-mushrooms\",\"Position\":[4,5,6],\"Heading\":1}]");

            Assert.IsTrue(collection.Load(path));
            Assert.IsFalse(retainedHardPointWasMissing);
            Assert.AreEqual(4f, collection[1].Position[0]);
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
        var path = Path.Combine(directory, "CollectionNodeSpawns.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "[]");

        try
        {
            var collection = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(collection.Load(path));
            Assert.IsTrue(collection.TryAddPersistent("briarwood-mushrooms", new Vector4(1, 2, 3, 1), 0.5f, out var added));

            var reloaded = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(reloaded.Load(path));
            Assert.IsTrue(reloaded.TryGetValue(added.Id, out var persisted));
            Assert.AreEqual("briarwood-mushrooms", persisted.Pool);
            Assert.AreEqual(2f, persisted.Position[1]);

            Assert.IsTrue(reloaded.TryRemovePersistent(added.Id));
            var empty = new CollectionNodeSpawnDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(empty.Load(path));
            Assert.AreEqual(0, empty.Count);
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
