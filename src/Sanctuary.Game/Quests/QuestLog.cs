using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Sanctuary.Game.Quests;

public sealed class QuestLog
{
    private readonly ConcurrentDictionary<int, QuestProgress> _quests = new();

    public int ActiveQuestId { get; set; }

    public int PendingTurnInQuestId { get; set; }

    public DateTime LastAcceptedAt { get; set; }

    public IEnumerable<QuestProgress> All => _quests.Values;

    public IEnumerable<QuestProgress> InProgress => _quests.Values.Where(quest => !quest.Completed);

    public int CompletedCount => _quests.Values.Count(quest => quest.Completed);

    public bool Contains(int questId) => _quests.ContainsKey(questId);

    public bool IsCompleted(int questId) => _quests.TryGetValue(questId, out var quest) && quest.Completed;

    public bool TryGetInProgress(int questId, [MaybeNullWhen(false)] out QuestProgress quest)
        => _quests.TryGetValue(questId, out quest) && !quest.Completed;

    public void Add(QuestProgress quest) => _quests[quest.QuestId] = quest;

    public bool Remove(int questId) => _quests.TryRemove(questId, out _);
}
