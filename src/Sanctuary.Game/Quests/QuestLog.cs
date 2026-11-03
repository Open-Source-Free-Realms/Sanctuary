using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace Sanctuary.Game.Quests;

public sealed class QuestLog
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, QuestProgress> _quests = [];
    private readonly Dictionary<int, QuestProgress> _inProgress = [];

    public int ActiveQuestId { get; set; }

    public int PendingTurnInQuestId { get; set; }

    public DateTime LastAcceptedAt { get; set; }

    public IReadOnlyList<QuestProgress> All
    {
        get
        {
            lock (_lock)
                return _quests.Values.ToList();
        }
    }

    public IReadOnlyList<QuestProgress> InProgress
    {
        get
        {
            lock (_lock)
                return _inProgress.Values.ToList();
        }
    }

    public int CompletedCount
    {
        get
        {
            lock (_lock)
                return _quests.Count - _inProgress.Count;
        }
    }

    public bool Contains(int questId)
    {
        lock (_lock)
            return _quests.ContainsKey(questId);
    }

    public bool IsCompleted(int questId)
    {
        lock (_lock)
            return _quests.ContainsKey(questId) && !_inProgress.ContainsKey(questId);
    }

    public bool TryGetInProgress(int questId, [MaybeNullWhen(false)] out QuestProgress quest)
    {
        lock (_lock)
            return _inProgress.TryGetValue(questId, out quest);
    }

    public void Add(QuestProgress quest)
    {
        lock (_lock)
        {
            _quests[quest.QuestId] = quest;

            if (quest.Completed)
                _inProgress.Remove(quest.QuestId);
            else
                _inProgress[quest.QuestId] = quest;
        }
    }

    public void AddCompleted(int questId)
    {
        var quest = new QuestProgress(questId);
        quest.Complete();
        Add(quest);
    }

    public void Complete(int questId)
    {
        lock (_lock)
        {
            if (_inProgress.Remove(questId, out var quest))
                quest.Complete();
        }
    }

    public bool Remove(int questId)
    {
        lock (_lock)
        {
            _inProgress.Remove(questId);
            return _quests.Remove(questId);
        }
    }
}
