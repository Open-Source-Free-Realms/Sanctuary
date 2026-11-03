using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.QuestGoals;

namespace Sanctuary.Game.Resources;

public class QuestDefinitionCollection
{
    private readonly ILogger _logger;

    public ConcurrentDictionary<int, QuestDefinition> Quests { get; } = new();

    // NPC definition id -> the quests it offers, and the quests it is a goal target or turn-in for.
    public ConcurrentDictionary<int, List<int>> ByGiver { get; } = new();
    public ConcurrentDictionary<int, List<int>> ByTarget { get; } = new();
    public ConcurrentDictionary<int, List<int>> ByStateNpc { get; } = new();

    public QuestDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool TryGet(int questId, out QuestDefinition definition) => Quests.TryGetValue(questId, out definition!);

    public bool TryGetNpcCursorId(int npcId, out byte cursorId)
    {
        cursorId = 0;

        foreach (var index in new[] { ByGiver, ByTarget })
        {
            if (!index.TryGetValue(npcId, out var questIds))
                continue;

            foreach (var questId in questIds)
                if (TryGet(questId, out var quest))
                    foreach (var goal in quest.Goals)
                        if (goal.CursorId != 0)
                        {
                            cursorId = goal.CursorId;
                            return true;
                        }
        }

        return false;
    }

    public bool TryGetNpcInteractRange(int npcId, out int interactRange)
    {
        interactRange = int.MaxValue;

        foreach (var index in new[] { ByGiver, ByTarget })
        {
            if (!index.TryGetValue(npcId, out var questIds))
                continue;

            foreach (var questId in questIds)
                if (TryGet(questId, out var quest))
                    foreach (var goal in quest.Goals)
                        if (goal.InteractRange > 0)
                            interactRange = Math.Min(interactRange, goal.InteractRange);
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
                AllowTrailingCommas = true,
                AllowOutOfOrderMetadataProperties = true
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

                if (entry.GiverNpcId != 0)
                    ByGiver.GetOrAdd(entry.GiverNpcId, _ => []).Add(entry.QuestId);

                if (entry.TargetNpcId != 0)
                    ByTarget.GetOrAdd(entry.TargetNpcId, _ => []).Add(entry.QuestId);

                IndexGoals(entry, filePath);

                foreach (var stateNpcId in entry.StateNpcIds())
                    ByStateNpc.GetOrAdd(stateNpcId, _ => []).Add(entry.QuestId);
            }

            EnforceSymmetricExclusions(filePath);

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
            // NameId doubles as the client's objective row key, so duplicates make goals indistinguishable.
            if (!goalNameIds.Add(goal.NameId))
                _logger.LogWarning("Duplicate goal NameId {nameId} on quest {id} in \"{file}\".", goal.NameId, quest.QuestId, filePath);

            switch (goal)
            {
                case TalkToNpcGoal talkGoal:
                    if (talkGoal.Targets.Count == 0)
                        talkGoal.Targets.Add(new TalkTarget { NpcId = quest.TargetNpcId });

                    // Intermediate goals can point at NPCs that are neither the giver nor the turn-in target, and
                    // every NPC of a counted talk goal has to be clickable, so index them all.
                    foreach (var targetNpcId in talkGoal.AllTalkTargetNpcIds())
                    {
                        var questIds = ByTarget.GetOrAdd(targetNpcId, _ => []);

                        if (!questIds.Contains(quest.QuestId))
                            questIds.Add(quest.QuestId);
                    }

                    if (!talkGoal.AllTalkTargetNpcIds().Any())
                        _logger.LogWarning("Talk goal on quest {id} has no NPC to talk to in \"{file}\"; it can never be credited.", quest.QuestId, filePath);
                    break;

                case ReachLocationGoal reachGoal:
                    if (reachGoal.ReachPosition == Vector3.Zero)
                        _logger.LogWarning("Reach goal on quest {id} has no ReachPosition in \"{file}\"; it can never be credited.", quest.QuestId, filePath);
                    break;

                case CollectGoal collectGoal:
                    if (string.IsNullOrEmpty(collectGoal.CollectNodeType))
                        _logger.LogWarning("Collect goal on quest {id} has no CollectNodeType in \"{file}\"; it can never be credited.", quest.QuestId, filePath);

                    if (collectGoal.RequiredCount <= 0)
                        _logger.LogWarning("Collect goal on quest {id} has no RequiredCount in \"{file}\"; it can never be completed.", quest.QuestId, filePath);
                    break;
            }
        }
    }

    private void EnforceSymmetricExclusions(string filePath)
    {
        foreach (var quest in Quests.Values)
        {
            foreach (var excludedId in quest.ExcludesQuestIds)
            {
                if (!Quests.TryGetValue(excludedId, out var excluded))
                {
                    _logger.LogWarning("Quest {id} excludes unknown quest {excludedId} in \"{file}\".", quest.QuestId, excludedId, filePath);
                    continue;
                }

                if (excluded.ExcludesQuestIds.Contains(quest.QuestId))
                    continue;

                excluded.ExcludesQuestIds.Add(quest.QuestId);
                _logger.LogWarning("Quest {id} excludes quest {excludedId}, which did not exclude it back; added the missing exclusion. \"{file}\"", quest.QuestId, excludedId, filePath);
            }
        }
    }
}
