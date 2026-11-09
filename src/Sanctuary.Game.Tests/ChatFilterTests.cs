using System;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ChatFilterTests
{
    private static readonly ChatFilterList Off = ChatFilterList.Empty;

    // builds an enabled list from patterns
    private static ChatFilterList List(params string[] patterns) => new(true,
        Array.ConvertAll(patterns, x => new Regex(x, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, ChatFilterCollection.MatchTimeout)));

    [TestMethod]
    public void Censor_ReplacesEachMatchedCharacter()
    {
        Assert.AreEqual("this is ####!", ChatFilter.Censor("this is darn!", List(@"\bdarn\b"), Off));
    }

    [TestMethod]
    public void Censor_IgnoresCase()
    {
        Assert.AreEqual("####", ChatFilter.Censor("DaRn", List(@"\bdarn\b"), Off));
    }

    [TestMethod]
    public void Censor_LeavesMessageAlone_WhenBlacklistDisabledOrEmpty()
    {
        var disabled = new ChatFilterList(false, List("darn").Patterns);

        Assert.AreEqual("darn", ChatFilter.Censor("darn", disabled, Off));
        Assert.AreEqual("darn", ChatFilter.Censor("darn", new ChatFilterList(true, []), Off));
    }

    [TestMethod]
    public void Censor_CatchesPunctuationInsideWords_AndCensorsTheSeparators()
    {
        Assert.AreEqual("say ####### now", ChatFilter.Censor("say d.a-r_n now", List(@"\bdarn\b"), Off));
        Assert.AreEqual("#####", ChatFilter.Censor("d​arn", List(@"\bdarn\b"), Off));
    }

    [TestMethod]
    public void Censor_StillMatchesOriginalText_WhenStrippingJoinsWords()
    {
        // stripped is "hidarn", so only the original text matches
        Assert.AreEqual("hi.####", ChatFilter.Censor("hi.darn", List(@"\bdarn\b"), Off));
    }

    [TestMethod]
    public void Censor_KeepsLetterLikeSymbols_ForPatternsToMatch()
    {
        Assert.AreEqual("####", ChatFilter.Censor("d@rn", List(@"\bd[a@]rn\b"), Off));
    }

    [TestMethod]
    public void Censor_DoesNotJoinWordsAcrossSpaces()
    {
        Assert.AreEqual("da rn", ChatFilter.Censor("da rn", List(@"\bdarn\b"), Off));
    }

    [TestMethod]
    public void Censor_SkipsMatchesInsideWhitelistMatches()
    {
        var blacklist = List("ass");
        var whitelist = List(@"\bclass\b", @"\bpass\b");

        Assert.AreEqual("class pass ###", ChatFilter.Censor("class pass ass", blacklist, whitelist));
    }

    [TestMethod]
    public void Censor_IgnoresWhitelist_WhenDisabled()
    {
        var whitelist = new ChatFilterList(false, List(@"\bclass\b").Patterns);

        Assert.AreEqual("cl###", ChatFilter.Censor("class", List("ass"), whitelist));
    }

    [TestMethod]
    public void Censor_SkipsPatternThatTimesOut()
    {
        var slow = new Regex("(a+)+$", RegexOptions.None, TimeSpan.FromMilliseconds(1));
        var blacklist = new ChatFilterList(true, [slow, new Regex("darn", RegexOptions.IgnoreCase, ChatFilterCollection.MatchTimeout)]);

        var message = new string('a', 5000) + "! darn";

        Assert.EndsWith("! ####", ChatFilter.Censor(message, blacklist, Off));
    }

    [TestMethod]
    public void IsBlocked_MatchesInsideNames_AndRespectsWhitelist()
    {
        var blacklist = List("ass");

        Assert.IsTrue(ChatFilter.IsBlocked("Big Ass", blacklist, Off));
        Assert.IsTrue(ChatFilter.IsBlocked("A.s.s Kicker", blacklist, Off));
        Assert.IsFalse(ChatFilter.IsBlocked("Bass Fisher", blacklist, List(@"\bbass\b")));
        Assert.IsFalse(ChatFilter.IsBlocked("Big Ass", new ChatFilterList(false, blacklist.Patterns), Off), "blacklist off");
    }

    [TestMethod]
    public void IsBlocked_WholeWordPatternsMissJoinedNames()
    {
        Assert.IsFalse(ChatFilter.IsBlocked("DarnKing", List(@"\bdarn\b"), Off));
        Assert.IsTrue(ChatFilter.IsBlocked("DarnKing", List("darn"), Off));
    }

    [TestMethod]
    public void Load_MergesDefaultAndLocalFiles()
    {
        using var files = new TempFiles();
        files.WriteDefault("""
            // comment lines are allowed
            { "Enabled": false, "Entries": [ { "Key": "one", "Pattern": "one" }, { "Key": "two", "Pattern": "two" } ] }
            """);
        files.WriteLocal("""
            { "Enabled": true, "Add": [ { "Key": "three", "Pattern": "three", "Note": "added" } ], "Remove": [ "two" ] }
            """);

        var collection = new ChatFilterCollection(NullLogger.Instance);

        Assert.IsTrue(collection.Load(files.Default, files.Local));
        Assert.IsTrue(collection.Current.Enabled);
        CollectionAssert.AreEqual(new[] { "one", "three" }, Array.ConvertAll([.. collection.Current.Patterns], x => x.ToString()));
    }

    [TestMethod]
    public void Load_UsesDefaults_WhenLocalFileIsMissing()
    {
        using var files = new TempFiles();
        files.WriteDefault("""{ "Enabled": false, "Entries": [] }""");

        var collection = new ChatFilterCollection(NullLogger.Instance);

        Assert.IsTrue(collection.Load(files.Default, files.Local));
        Assert.IsFalse(collection.Current.Enabled);
        Assert.IsEmpty(collection.Current.Patterns);
    }

    [TestMethod]
    public void Load_SkipsInvalidPatterns()
    {
        using var files = new TempFiles();
        files.WriteDefault("""{ "Enabled": true, "Entries": [ { "Key": "broken", "Pattern": "(" }, { "Key": "ok", "Pattern": "ok" } ] }""");

        var collection = new ChatFilterCollection(NullLogger.Instance);

        Assert.IsTrue(collection.Load(files.Default, files.Local));
        Assert.HasCount(1, collection.Current.Patterns);
    }

    [TestMethod]
    public void Load_FailsWhenDefaultFileIsMissing()
    {
        using var files = new TempFiles();

        Assert.IsFalse(new ChatFilterCollection(NullLogger.Instance).Load(files.Default, files.Local));
    }

    [TestMethod]
    public void Load_BrokenLocalFile_UsesDefaultsFirst_ThenKeepsPreviousOnReload()
    {
        using var files = new TempFiles();
        files.WriteDefault("""{ "Enabled": false, "Entries": [] }""");
        files.WriteLocal("{ not json");

        var collection = new ChatFilterCollection(NullLogger.Instance);

        Assert.IsTrue(collection.Load(files.Default, files.Local));
        Assert.IsFalse(collection.Current.Enabled);

        files.WriteLocal("""{ "Enabled": true, "Add": [ { "Key": "darn", "Pattern": "darn" } ] }""");
        Assert.IsTrue(collection.Load(files.Default, files.Local));
        Assert.IsTrue(collection.Current.Enabled);

        files.WriteLocal("{ not json");
        Assert.IsTrue(collection.Load(files.Default, files.Local));
        Assert.IsTrue(collection.Current.Enabled);
        Assert.HasCount(1, collection.Current.Patterns);
    }

    [TestMethod]
    public void AddEntry_CreatesLocalFile_AndAppliesImmediately()
    {
        using var files = new TempFiles();
        files.WriteDefault("""{ "Enabled": false, "Entries": [] }""");

        var collection = new ChatFilterCollection(NullLogger.Instance);
        collection.Load(files.Default, files.Local);

        Assert.IsTrue(collection.AddEntry("darn", @"\bdarn\b", out _));
        Assert.IsTrue(collection.SetEnabled(true, out _));

        Assert.IsTrue(File.Exists(files.Local));
        Assert.AreEqual("####", ChatFilter.Censor("darn", collection.Current, Off));

        Assert.IsFalse(collection.AddEntry("darn", @"\bdarn\b", out _), "duplicate");
        Assert.IsFalse(collection.AddEntry("broken", "(", out _), "invalid regex");
    }

    [TestMethod]
    public void RemoveEntry_DeletesLocalEntries_AndHidesDefaultEntries()
    {
        using var files = new TempFiles();
        files.WriteDefault("""{ "Enabled": true, "Entries": [ { "Key": "one", "Pattern": "one" } ] }""");

        var collection = new ChatFilterCollection(NullLogger.Instance);
        collection.Load(files.Default, files.Local);
        collection.AddEntry("two", "two", out _);

        Assert.IsTrue(collection.RemoveEntry("two", out _));
        Assert.IsTrue(collection.RemoveEntry("one", out _));
        Assert.IsEmpty(collection.Current.Patterns);
        Assert.IsFalse(collection.RemoveEntry("one", out _), "already removed");

        // adding a removed default brings it back
        Assert.IsTrue(collection.AddEntry("one", "one", out _));
        Assert.HasCount(1, collection.Current.Patterns);
    }

    [TestMethod]
    public void Edits_RefuseToOverwriteBrokenLocalFile()
    {
        using var files = new TempFiles();
        files.WriteDefault("""{ "Enabled": false, "Entries": [] }""");
        files.WriteLocal("{ not json");

        var collection = new ChatFilterCollection(NullLogger.Instance);
        collection.Load(files.Default, files.Local);

        Assert.IsFalse(collection.AddEntry("darn", "darn", out _));
        Assert.AreEqual("{ not json", File.ReadAllText(files.Local));
    }

    [TestMethod]
    public void Load_LocalEntryReplacesDefaultWithSameKey_AndSkipsEntriesWithoutKey()
    {
        using var files = new TempFiles();
        files.WriteDefault("""{ "Enabled": true, "Entries": [ { "Key": "darn", "Pattern": "darn" }, { "Pattern": "nokey" } ] }""");
        files.WriteLocal("""{ "Add": [ { "Key": "DARN", "Pattern": "d[a@]rn" } ] }""");

        var collection = new ChatFilterCollection(NullLogger.Instance);

        Assert.IsTrue(collection.Load(files.Default, files.Local));
        Assert.HasCount(1, collection.Current.Patterns);
        Assert.AreEqual("d[a@]rn", collection.Current.Patterns[0].ToString());
    }

    [TestMethod]
    public void ShippedDefaultFiles_LoadEmptyAndDisabled()
    {
        var resources = FindResourcesDirectory();

        foreach (var name in new[] { "ChatBlacklist", "ChatWhitelist" })
        {
            var collection = new ChatFilterCollection(NullLogger.Instance);

            Assert.IsTrue(collection.Load(Path.Combine(resources, $"{name}.json"), Path.Combine(resources, $"{name}.missing.json")));
            Assert.IsFalse(collection.Current.Enabled);
            Assert.IsEmpty(collection.Current.Patterns);
        }
    }

    // walks up from the test output to find src/Resources
    private static string FindResourcesDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Resources");

            if (File.Exists(Path.Combine(candidate, "ChatBlacklist.json")))
                return candidate;
        }

        throw new DirectoryNotFoundException("Could not find src/Resources.");
    }

    // a default/local file pair in a temp folder, deleted after the test
    private sealed class TempFiles : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"chatfilter-{Guid.NewGuid():N}");

        public TempFiles() => Directory.CreateDirectory(_directory);

        public string Default => Path.Combine(_directory, "ChatBlacklist.json");
        public string Local => Path.Combine(_directory, "ChatBlacklist.local.json");

        public void WriteDefault(string json) => File.WriteAllText(Default, json);
        public void WriteLocal(string json) => File.WriteAllText(Local, json);

        public void Dispose() => Directory.Delete(_directory, true);
    }
}
