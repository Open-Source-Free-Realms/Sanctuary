using System;
using System.IO;
using System.Reflection;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ResourceManagerTests
{
    [TestMethod]
    public void FileSystemWatcherChange_IgnoresAtomicSaveTemporaryFileAfterMove()
    {
        Directory.CreateDirectory(ResourceManager.BaseDirectory);
        var resourceManager = new ResourceManager(NullLogger<ResourceManager>.Instance);

        try
        {
            var handler = typeof(ResourceManager).GetMethod(
                "_fileSystemWatcher_Changed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var eventArgs = new FileSystemEventArgs(
                WatcherChangeTypes.Changed,
                ResourceManager.BaseDirectory,
                $"deleted-{Guid.NewGuid():N}.json.tmp");

            Assert.IsNotNull(handler);
            handler.Invoke(resourceManager, new object[] { resourceManager, eventArgs });
        }
        finally
        {
            GetWatcher(resourceManager).Dispose();
        }
    }

    [TestMethod]
    public void FileSystemWatcherChange_ReloadsCollectionNodeSpawnDirectory()
    {
        const int zoneDefinitionId = 999999;
        const int spawnId = 999999;
        var zoneDirectory = Path.Combine(ResourceManager.CollectionNodeSpawnsDirectory, zoneDefinitionId.ToString());
        Directory.CreateDirectory(zoneDirectory);
        var filePath = Path.Combine(zoneDirectory, "watcher-test.json");
        var resourceManager = new ResourceManager(NullLogger<ResourceManager>.Instance);
        var watcher = GetWatcher(resourceManager);
        watcher.EnableRaisingEvents = false;
        File.WriteAllText(filePath,
            $"[{{\"Id\":{spawnId},\"Position\":[1,2,3],\"Heading\":0}}]");

        try
        {
            var handler = typeof(ResourceManager).GetMethod(
                "_fileSystemWatcher_Changed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            // Persistent resource saves may move the temporary file before its watcher callback runs.
            var eventArgs = new FileSystemEventArgs(
                WatcherChangeTypes.Changed,
                zoneDirectory,
                Path.GetFileName(filePath));

            Assert.IsNotNull(handler);
            handler.Invoke(resourceManager, new object[] { resourceManager, eventArgs });
            Assert.IsTrue(resourceManager.CollectionNodeSpawns.TryGetValue(spawnId, out var spawn));
            Assert.AreEqual("watcher-test", spawn.Pool);
            Assert.AreEqual(zoneDefinitionId, spawn.ZoneDefinitionId);
        }
        finally
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            Directory.Delete(zoneDirectory, true);
        }
    }

    private static FileSystemWatcher GetWatcher(ResourceManager resourceManager)
    {
        var watcherField = typeof(ResourceManager).GetField(
            "_fileSystemWatcher",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (FileSystemWatcher)watcherField!.GetValue(resourceManager)!;
    }
}
