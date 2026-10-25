using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Sanctuary.Game.Quests;

public sealed class QuestLog
{
    private readonly ConcurrentDictionary<int, QuestProgress> _quests = new();
    private readonly ConcurrentDictionary<int, QuestProgress> _inProgress = new();

    public int ActiveQuestId { get; set; }

    public int PendingTurnInQuestId { get; set; }

    public DateTime LastAcceptedAt { get; set; }

    public IEnumerable<QuestProgress> All => _quests.Values;

    public IEnumerable<QuestProgress> InProgress => _inProgress.Values;

    public int CompletedCount => _quests.Count - _inProgress.Count;

    public bool Contains(int questId) => _quests.ContainsKey(questId);

    public bool IsCompleted(int questId) => _quests.ContainsKey(questId) && !_inProgress.ContainsKey(questId);

    public bool TryGetInProgress(int questId, [MaybeNullWhen(false)] out QuestProgress quest)
        => _inProgress.TryGetValue(questId, out quest);

    public void Add(QuestProgress quest)
    {
        _quests[quest.QuestId] = quest;

        if (quest.Completed)
            _inProgress.TryRemove(quest.QuestId, out _);
        else
            _inProgress[quest.QuestId] = quest;
    }

    public void AddCompleted(int questId)
    {
        var quest = new QuestProgress(questId);
        quest.Complete();
        Add(quest);
    }

    public void Complete(int questId)
    {
        if (_inProgress.TryRemove(questId, out var quest))
            quest.Complete();
    }

    public bool Remove(int questId)
    {
        _inProgress.TryRemove(questId, out _);
        return _quests.TryRemove(questId, out _);
    }
}
