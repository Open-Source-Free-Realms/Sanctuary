using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Conversations;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class GmContentTests
{
    [TestMethod]
    public void PanelText_RoundTrips()
    {
        const string text = "if a | b then\n  print(\"C:\\\\x\") end";

        var escaped = PanelTextHelper.Escape(text);

        Assert.IsFalse(escaped.Contains('|'));
        Assert.IsFalse(escaped.Contains('\n'));
        Assert.AreEqual(text, PanelTextHelper.Unescape(escaped));
    }

    [TestMethod]
    public void PanelText_UnescapesSpacesAndTabs()
    {
        // Script chunks arrive as one chat token, so the panel escapes whitespace.
        Assert.AreEqual("a  b\tc", PanelTextHelper.Unescape(@"a\s\sb\tc"));
    }

    [TestMethod]
    public void Conversation_RoundTripsThroughJson()
    {
        var conversation = new Conversation { Start = 2 };
        conversation.Nodes.Add(new ConversationNode
        {
            Id = 2,
            Text = "Hello!",
            Choices = { new ConversationChoice { Text = "Bye", Next = 0, GiveItemId = 5, GiveQuantity = 3, Hook = "wave" } }
        });

        var copy = Conversation.FromJson(conversation.ToJson());

        Assert.IsNotNull(copy);
        Assert.AreEqual(2, copy.Start);
        Assert.AreEqual("Hello!", copy.GetNode(2)!.Text);
        Assert.AreEqual("wave", copy.GetNode(2)!.Choices[0].Hook);
        Assert.AreEqual(3, copy.GetNode(2)!.Choices[0].GiveQuantity);
    }

    [TestMethod]
    public void Conversation_InvalidJsonIsNull()
    {
        Assert.IsNull(Conversation.FromJson("{ not json"));
        Assert.IsNull(Conversation.FromJson(null));
    }
}
