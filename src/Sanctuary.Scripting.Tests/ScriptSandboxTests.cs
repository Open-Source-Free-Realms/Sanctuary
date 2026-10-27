using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Scripting.Tests;

[TestClass]
public class ScriptSandboxTests
{
    private sealed class MockScriptPlayer : IScriptablePlayer
    {
        public ulong Guid => 42;
        public string Name => "Tester";
        public (float x, float y, float z) Position => (1, 2, 3);

        public List<string> Messages { get; } = [];
        public List<(int Item, int Quantity)> Given { get; } = [];

        public void Message(string message) => Messages.Add(message);
        public void GiveItem(int itemDefinitionId, int quantity) => Given.Add((itemDefinitionId, quantity));
    }

    private static MockScriptNpc CreateNpc() => new(new MockScriptZone(NullLogger.Instance));

    [TestMethod]
    public async Task Hooks_ReceivePlayerAndUseApi()
    {
        var npc = CreateNpc();
        var player = new MockScriptPlayer();

        var (sandbox, error) = await ScriptSandbox.CompileAsync(npc, """
            function onInteract(player)
                player:message("Hi " .. player.name)
                player:giveItem(7, 2)
                npc:emote(3333)
            end
            """, "test");

        Assert.IsNull(error);
        Assert.IsNotNull(sandbox);
        Assert.IsTrue(sandbox.HasHook("onInteract"));
        Assert.IsFalse(sandbox.HasHook("onSecond"));

        Assert.IsNull(await sandbox.CallAsync("onInteract", player));

        CollectionAssert.AreEqual(new[] { "Hi Tester" }, player.Messages);
        Assert.AreEqual((7, 2), player.Given[0]);
        Assert.AreEqual((3333, false), npc.Emotes[0]);
    }

    [TestMethod]
    public async Task UnsafeLibraries_AreNotAvailable()
    {
        foreach (var source in new[] { "os.exit()", "io.write('x')", "load('return 1')", "require('x')", "debug.traceback()" })
        {
            var (sandbox, error) = await ScriptSandbox.CompileAsync(CreateNpc(), source, "unsafe");

            Assert.IsNull(sandbox, source);
            Assert.IsNotNull(error, source);
        }
    }

    [TestMethod]
    public async Task EndlessLoop_IsStopped()
    {
        var (sandbox, error) = await ScriptSandbox.CompileAsync(CreateNpc(), "function onSecond() while true do end end", "loop");

        Assert.IsNull(error);
        Assert.IsNotNull(sandbox);

        var callError = await sandbox.CallAsync("onSecond");

        Assert.IsNotNull(callError);
        StringAssert.Contains(callError, "Stopped");
    }

    [TestMethod]
    public async Task CompileError_IsReported()
    {
        var (sandbox, error) = await ScriptSandbox.CompileAsync(CreateNpc(), "function onSpawn( end", "broken");

        Assert.IsNull(sandbox);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public async Task SandboxedLibraries_AreIsolated()
    {
        var npc = CreateNpc();

        var (first, _) = await ScriptSandbox.CompileAsync(npc, "string.upper = nil", "first");
        var (second, error) = await ScriptSandbox.CompileAsync(npc, "x = string.upper('a')", "second");

        Assert.IsNotNull(first);
        Assert.IsNull(error);
        Assert.IsNotNull(second);
    }
}
