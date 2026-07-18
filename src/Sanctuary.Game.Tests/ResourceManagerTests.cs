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
    public void FileSystemWatcherChange_IgnoresDeletedTemporaryFile()
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
            var watcherField = typeof(ResourceManager).GetField(
                "_fileSystemWatcher",
                BindingFlags.Instance | BindingFlags.NonPublic);
            (watcherField?.GetValue(resourceManager) as FileSystemWatcher)?.Dispose();
        }
    }
}
