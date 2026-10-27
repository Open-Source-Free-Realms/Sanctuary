using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.ChatCommands;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ChatCommandTextTests
{
    [TestMethod]
    public void Remainder_KeepsRunsOfSpacesAndTrailingSpace()
    {
        Assert.AreEqual("    if x  then ", ChatCommandText.Remainder("script append     if x  then ", 2));
    }

    [TestMethod]
    public void Remainder_SkipsOnlyOneSeparatorSpace()
    {
        Assert.AreEqual("Hello  there", ChatCommandText.Remainder("node 3 Hello  there", 2));
        Assert.AreEqual(" indented", ChatCommandText.Remainder("append  indented", 1));
    }

    [TestMethod]
    public void Remainder_IsEmptyWhenNothingFollows()
    {
        Assert.AreEqual(string.Empty, ChatCommandText.Remainder("commit", 1));
        Assert.AreEqual(string.Empty, ChatCommandText.Remainder("node 3", 2));
    }
}
