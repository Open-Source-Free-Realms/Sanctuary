using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class QuestDefinitionCollection
{
    private readonly ILogger _logger;

    public ConcurrentDictionary<int, QuestDefinition> Quests { get; } = new();

    // NPC guid -> the quests it offers, and the quests it is a goal target or turn-in for.
    public ConcurrentDictionary<ulong, List<int>> ByGiver { get; } = new();
    public ConcurrentDictionary<ulong, List<int>> ByTarget { get; } = new();

    public QuestDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool TryGet(int questId, out QuestDefinition definition) => Quests.TryGetValue(questId, out definition!);

    public bool TryGetNpcInteractRange(ulong npcGuid, out int interactRange)
    {
        interactRange = int.MaxValue;

        foreach (var index in new[] { ByGiver, ByTarget })
        {
            if (!index.TryGetValue(npcGuid, out var questIds))
                continue;

            foreach (var questId in questIds)
                if (TryGet(questId, out var quest) && quest.NpcInteractRange > 0)
                    interactRange = Math.Min(interactRange, quest.NpcInteractRange);
        }

        if (interactRange == int.MaxValue)
        {
            interactRange = 0;
            return false;
        }

        return true;
    }

    public bool Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Failed to find file \"{file}\". No quests will be loaded.", filePath);
            return true;
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var entries = JsonSerializer.Deserialize<List<QuestDefinition>>(fileStream, jsonSerializerOptions);

            if (entries is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            foreach (var entry in entries)
            {
                if (entry.Goals.Count == 0)
                {
                    _logger.LogError("Quest {id} has no goals. \"{file}\"", entry.QuestId, filePath);
                    return false;
                }

                if (!Quests.TryAdd(entry.QuestId, entry))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", entry.QuestId, filePath);
                    continue;
                }

                if (entry.GiverGuid != 0)
                    ByGiver.GetOrAdd(entry.GiverGuid, _ => []).Add(entry.QuestId);

                if (entry.TargetGuid != 0)
                    ByTarget.GetOrAdd(entry.TargetGuid, _ => []).Add(entry.QuestId);

                IndexGoals(entry, filePath);
            }

            _logger.LogInformation("Loaded {count} quest definitions from \"{file}\".", Quests.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        return true;
    }

    private void IndexGoals(QuestDefinition quest, string filePath)
    {
        var goalNameIds = new HashSet<int>();

        foreach (var goal in quest.Goals)
        {
            // Intermediate goals can point at NPCs that are neither the giver nor the turn-in target, and
            // every NPC of a counted talk goal has to be clickable, so index them all.
            foreach (var targetGuid in goal.AllTalkTargetGuids())
            {
                var questIds = ByTarget.GetOrAdd(targetGuid, _ => []);

                if (!questIds.Contains(quest.QuestId))
                    questIds.Add(quest.QuestId);
            }

            // NameId doubles as the client's objective row key, so duplicates make goals indistinguishable.
            if (!goalNameIds.Add(goal.NameId))
                _logger.LogWarning("Duplicate goal NameId {nameId} on quest {id} in \"{file}\".", goal.NameId, quest.QuestId, filePath);

            if (goal.Type == QuestGoalType.Collect && goal.RequiredCount <= 0)
                goal.RequiredCount = goal.CollectSpawns.Count;
        }
    }
}
