using System;
using System.Collections.Generic;

using Sanctuary.Game.Resources.Definitions.QuestGoals;

namespace Sanctuary.Game.Quests;

public enum QuestConversationStep
{
    Next,
    Finished,
    Abandoned
}

public sealed class QuestConversation
{
    private readonly List<QuestDialogueLine> _remaining = [];
    private Action? _onFinished;

    public QuestDialogueLine? Current { get; private set; }

    public ulong NpcGuid { get; private set; }

    public ulong TalkingNpcGuid { get; set; }

    public DateTimeOffset TalkAnimationEndsAt { get; set; }

    public bool IsLastLine => Current is { Choices.Count: 0 } && _remaining.Count == 0;

    public void Start(IReadOnlyList<QuestDialogueLine> lines, ulong npcGuid, Action? onFinished = null)
    {
        Clear();

        if (lines.Count == 0)
            return;

        _remaining.AddRange(lines);
        Current = _remaining[0];
        _remaining.RemoveAt(0);

        NpcGuid = npcGuid;
        _onFinished = onFinished;
    }

    public QuestConversationStep Answer(int responseId)
    {
        if (Current is null)
            return QuestConversationStep.Abandoned;

        if (Current.Choices.Count > 0)
        {
            var index = responseId - 1;

            if (index < 0 || index >= Current.Choices.Count)
                return QuestConversationStep.Abandoned;

            var choice = Current.Choices[index];

            if (choice.Retry)
                _remaining.Insert(0, Current);

            _remaining.InsertRange(0, choice.Then);
        }

        if (_remaining.Count == 0)
            return QuestConversationStep.Finished;

        Current = _remaining[0];
        _remaining.RemoveAt(0);

        return QuestConversationStep.Next;
    }

    public Action? Finish()
    {
        var onFinished = _onFinished;
        Clear();
        return onFinished;
    }

    public void Clear()
    {
        _remaining.Clear();
        _onFinished = null;
        Current = null;
        NpcGuid = 0;
    }
}
