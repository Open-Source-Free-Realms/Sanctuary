using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources.Definitions.QuestGoals;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class QuestConversationTests
{
    private const int Intro = 1;
    private const int Question = 2;
    private const int Wrong = 3;
    private const int Right = 4;
    private const int Outro = 5;

    [TestMethod]
    public void Answer_WalksPlainLinesInOrderThenFinishes()
    {
        var conversation = Start(Line(Intro), Line(Outro));

        Assert.AreEqual(Intro, conversation.Current!.TextId);
        Assert.IsFalse(conversation.IsLastLine);

        Assert.AreEqual(QuestConversationStep.Next, conversation.Answer(1));
        Assert.AreEqual(Outro, conversation.Current!.TextId);
        Assert.IsTrue(conversation.IsLastLine);

        Assert.AreEqual(QuestConversationStep.Finished, conversation.Answer(1));
    }

    [TestMethod]
    public void Answer_AWrongChoicePlaysItsLinesThenAsksTheQuestionAgain()
    {
        var conversation = Start(Quiz(), Line(Outro));

        Assert.AreEqual(QuestConversationStep.Next, conversation.Answer(1));
        Assert.AreEqual(Wrong, conversation.Current!.TextId);

        Assert.AreEqual(QuestConversationStep.Next, conversation.Answer(1));
        Assert.AreEqual(Question, conversation.Current!.TextId);
    }

    [TestMethod]
    public void Answer_TheRightChoicePlaysItsLinesThenMovesOn()
    {
        var conversation = Start(Quiz(), Line(Outro));

        Assert.AreEqual(QuestConversationStep.Next, conversation.Answer(2));
        Assert.AreEqual(Right, conversation.Current!.TextId);

        Assert.AreEqual(QuestConversationStep.Next, conversation.Answer(1));
        Assert.AreEqual(Outro, conversation.Current!.TextId);

        Assert.AreEqual(QuestConversationStep.Finished, conversation.Answer(1));
    }

    [TestMethod]
    public void Answer_AResponseTheQuestionDoesNotOfferAbandons()
    {
        var conversation = Start(Quiz());

        Assert.AreEqual(QuestConversationStep.Abandoned, conversation.Answer(0));
        Assert.AreEqual(QuestConversationStep.Abandoned, conversation.Answer(3));
    }

    [TestMethod]
    public void Finish_ReturnsTheCallbackOnceAndClears()
    {
        var finished = 0;
        var conversation = new QuestConversation();
        conversation.Start([Line(Outro)], 7, () => finished++);

        Assert.AreEqual(QuestConversationStep.Finished, conversation.Answer(1));
        conversation.Finish()?.Invoke();

        Assert.AreEqual(1, finished);
        Assert.IsNull(conversation.Current);
        Assert.IsNull(conversation.Finish());
    }

    private static QuestConversation Start(params QuestDialogueLine[] lines)
    {
        var conversation = new QuestConversation();
        conversation.Start(lines, 7);
        return conversation;
    }

    private static QuestDialogueLine Line(int textId) => new() { TextId = textId };

    private static QuestDialogueLine Quiz() => new()
    {
        TextId = Question,
        Choices =
        [
            new QuestDialogueChoice { ResponseTextId = 10, Retry = true, Then = new List<QuestDialogueLine> { Line(Wrong) } },
            new QuestDialogueChoice { ResponseTextId = 11, Then = new List<QuestDialogueLine> { Line(Right) } }
        ]
    };
}
