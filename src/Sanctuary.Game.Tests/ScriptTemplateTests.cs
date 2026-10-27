using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Authoring;
using Sanctuary.Scripting;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ScriptTemplateTests
{
    private sealed class StubNpc : IScriptableNpc
    {
        public ulong Guid => 1;
        public string? Name { get; set; } = "Stub";
        public IScriptableZone Zone { get; } = new StubZone();
        public (float x, float y, float z) Position => (0, 0, 0);
        public List<ulong> NearbyGuids { get; } = [];
        public ILogger Logger => NullLogger.Instance;
        public ScriptRuntime ScriptRuntime { get; } = new(NullLogger.Instance);

        public ScriptContext GetOrCreateScriptContext() => throw new NotSupportedException();
        public bool TryAddScript(string scriptName) => false;
        public bool TryRemoveScript(string scriptName) => false;
        public void Say(string message) { }
        public void SayLocalized(int stringId) { }
        public void MoveTo(float x, float y, float z, bool direct) { }
        public void Emote(int animationId, bool loop) { }
        public void Face(IScriptablePlayer player) { }
        public void StartConversation(IScriptablePlayer player, int? nodeId) { }
        public void SetCollidable(bool collidable) { }
        public bool IsMoving => false;
        public void SetHeading(float heading) { }
        public void SetSpeed(float speed) { }
        public IReadOnlyList<ulong> GetNearbyPlayerGuids(float radius) => NearbyGuids;
        public void PlayAnimation(int animationId) { }
        public void SetBaseAnimation(int animationId) { }
        public IReadOnlyList<ulong> GetPlayerGuidsOnTop(float horizontalRadius, float minHeight, float maxHeight) => NearbyGuids;
        public int AddEffectTag(int compositeEffectId) => 1;
        public void RemoveEffectTag(int tagId) { }
        public void PlayEffect(int compositeEffectId) { }
        public void Remove(int compositeEffectId) { }
    }

    private sealed class StubZone : IScriptableZone
    {
        public IScriptManager ScriptManager => null!;
        public int Id => 1;
        public string Name => "stub";
        public ILogger Logger => NullLogger.Instance;
        public ScriptRuntime ScriptRuntime => throw new NotSupportedException();

        public ScriptContext GetOrCreateScriptContext() => throw new NotSupportedException();
        public bool TryAddScript(string scriptName) => false;
        public bool TryRemoveScript(string scriptName) => false;
        public void SetClientAreaEnabled(int areaId, bool enabled) { }
        public void Broadcast(string message) { }
        public void BroadcastNear(string message, float x, float z, float radius) { }
        public bool IsSeasonActive(string seasonName) => false;

        public bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, float pitch, float roll, out IScriptableNpc npc)
        {
            npc = null!;
            return false;
        }
    }

    private sealed class StubPlayer : IScriptablePlayer
    {
        public ulong Guid => 7;
        public string Name => "Tester";
        public (float x, float y, float z) Position { get; set; }

        public void Message(string message) { }
        public void GiveItem(int itemDefinitionId, int quantity) { }
        public void AcceptQuest(int questId) { }
        public bool HasCompletedQuest(int questId) => false;
    }

    private const string Sample = """
        -- @name sample
        -- @description Says things
        -- @param text string Hi there
        -- @param count int 3
        function onInteract(player) npc:say("{{text}} x{{count}}") end
        """;

    [TestMethod]
    public void Parse_ReadsHeader()
    {
        var template = ScriptTemplate.Parse(Sample, null, null, true);

        Assert.AreEqual("sample", template.Name);
        Assert.AreEqual("Says things", template.Description);
        Assert.AreEqual(2, template.Params.Count);
        Assert.AreEqual(new ScriptTemplateParam("text", "string", "Hi there"), template.Params[0]);
    }

    [TestMethod]
    public void Render_UsesDefaultsAndValues()
    {
        var template = ScriptTemplate.Parse(Sample, null, null, true);

        Assert.IsTrue(template.TryRender(new Dictionary<string, string> { ["count"] = "5" }, out var source, out _));
        StringAssert.Contains(source, "npc:say(\"Hi there x5\")");
    }

    [TestMethod]
    public void Render_EscapesStringsForLua()
    {
        var template = ScriptTemplate.Parse(Sample, null, null, true);

        Assert.IsTrue(template.TryRender(new Dictionary<string, string> { ["text"] = "a\") os.exit() --" }, out var source, out _));
        StringAssert.Contains(source, "a\\\") os.exit() --");
    }

    [TestMethod]
    public void Render_RejectsBadNumbersAndUnknownKeys()
    {
        var template = ScriptTemplate.Parse(Sample, null, null, true);

        Assert.IsFalse(template.TryRender(new Dictionary<string, string> { ["count"] = "lots" }, out _, out var numberError));
        Assert.IsNotNull(numberError);

        Assert.IsFalse(template.TryRender(new Dictionary<string, string> { ["nope"] = "1" }, out _, out var keyError));
        Assert.IsNotNull(keyError);
    }

    [TestMethod]
    public void Render_RejectsPlaceholdersWithoutParam()
    {
        // Lua compiles "{{hits}}" as a nested table, so it has to fail here rather than at the first click.
        var template = ScriptTemplate.Parse("local health = {{hits}}\nfunction onInteract(player) health = health - 1 end", null, null, false);

        Assert.IsFalse(template.TryRender(new Dictionary<string, string>(), out _, out var error));
        StringAssert.Contains(error, "{{hits}}");
    }

    [TestMethod]
    public void Render_FillsLoadedTemplateWithDefaults()
    {
        var template = ScriptTemplate.Parse(File.ReadAllText(Path.Combine(FindTemplatesDirectory(), "pinata.lua")), null, null, false);

        Assert.IsTrue(template.TryRender(new Dictionary<string, string>(), out var source, out var error), error);
        StringAssert.Contains(source, "local health = 10");
        Assert.IsFalse(source.Contains("{{"), "No placeholder should reach Lua.");
    }

    [TestMethod]
    public async Task ShippedTemplates_CompileWithDefaults()
    {
        var directory = FindTemplatesDirectory();
        var files = Directory.GetFiles(directory, "*.lua");

        Assert.IsTrue(files.Length >= 9, "Expected the starter templates.");

        foreach (var file in files)
        {
            var template = ScriptTemplate.Parse(File.ReadAllText(file), null, null, true);

            Assert.IsTrue(ScriptTemplate.IsValidName(template.Name), $"{file} has no valid @name.");
            Assert.IsTrue(template.TryRender(new Dictionary<string, string>(), out var source, out var renderError), $"{file}: {renderError}");

            var (sandbox, compileError) = await ScriptSandbox.CompileAsync(new StubNpc(), source, template.Name);

            Assert.IsNotNull(sandbox, $"{file}: {compileError}");
        }
    }

    [TestMethod]
    public async Task ShippedTemplates_RunEveryHook()
    {
        foreach (var file in Directory.GetFiles(FindTemplatesDirectory(), "*.lua"))
        {
            var template = ScriptTemplate.Parse(File.ReadAllText(file), null, null, true);
            Assert.IsTrue(template.TryRender(new Dictionary<string, string>(), out var source, out _));

            var npc = new StubNpc();
            var player = new StubPlayer();
            var (sandbox, compileError) = await ScriptSandbox.CompileAsync(npc, source, template.Name);
            Assert.IsNotNull(sandbox, $"{file}: {compileError}");

            async Task Run(string hook, IScriptablePlayer? who = null, string? argument = null)
            {
                var error = await sandbox.CallAsync(hook, who, argument);
                Assert.IsNull(error, $"{template.Name} {hook}: {error}");
            }

            await Run("onSpawn");

            // A player clicks, walks off and is away a while, then comes back step by step and stays.
            await Run("onInteract", player);
            await Run("onChoice", player, "right");
            await Run("onChoice", player, "wrong");

            for (var second = 0; second < 200; second++)
            {
                var away = second < 40 ? 40f : Math.Max(0f, 40f - (second - 40));
                player.Position = (away, 0, 0);

                npc.NearbyGuids.Clear();
                if (away < 12)
                    npc.NearbyGuids.Add(player.Guid);

                if (away == 5)
                    await Run("onProximity", player);

                if (second % 25 == 0)
                    await Run("onInteract", player);

                await Run("onSecond");
            }
        }
    }

    private static string FindTemplatesDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Resources", "ScriptTemplates");

            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException("Resources/ScriptTemplates");
    }
}
