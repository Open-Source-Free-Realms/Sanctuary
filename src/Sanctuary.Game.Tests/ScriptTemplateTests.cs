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
        public IScriptableZone Zone => null!;
        public (float x, float y, float z) Position => (0, 0, 0);
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
        public IReadOnlyList<ulong> GetNearbyPlayerGuids(float radius) => [];
        public void PlayAnimation(int animationId) { }
        public void SetBaseAnimation(int animationId) { }
        public IReadOnlyList<ulong> GetPlayerGuidsOnTop(float horizontalRadius, float minHeight, float maxHeight) => [];
        public int AddEffectTag(int compositeEffectId) => 0;
        public void RemoveEffectTag(int tagId) { }
        public void PlayEffect(int compositeEffectId) { }
        public void Remove(int compositeEffectId) { }
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
