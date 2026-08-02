using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

// One collectible pickup to spawn for a Collect goal: a world object (interactable NPC) the player clicks
// to gather. Its guid is assigned at load time and maps back to (quest, goal) via
// Collectibles.
public sealed class CollectibleSpawn
{
    public ulong Guid { get; init; }
    public int ModelId { get; init; }
    public int NameId { get; init; }
    public Vector4 Position { get; init; }
}

// Loads quest definitions from Resources/Quests.json and builds the lookups the quest manager needs:
// by quest id, by giver NPC guid, and by target NPC guid.
public class QuestDefinitionCollection
{
    private readonly ILogger _logger;

    // questId -> definition.
    public ConcurrentDictionary<int, QuestDefinition> Quests { get; } = new();

    // giver NPC guid -> quest ids that NPC offers.
    public ConcurrentDictionary<ulong, List<int>> ByGiver { get; } = new();

    // target NPC guid -> quest ids that use the NPC as a talk-to / turn-in target.
    public ConcurrentDictionary<ulong, List<int>> ByTarget { get; } = new();

    // collectible pickup guid -> (questId, goalIndex) it credits when interacted with.
    public ConcurrentDictionary<ulong, (int QuestId, int GoalIndex)> Collectibles { get; } = new();

    // Every collectible pickup to spawn in the world (across all Collect goals of all quests).
    public List<CollectibleSpawn> CollectibleSpawns { get; } = new();

    // NameIds counted by any Kill goal. Not consumed yet - see QuestGoalType.Kill; a future combat
    // system would use this to know which world NPCs to spawn as attackable hostiles.
    public HashSet<int> KillTargetNameIds { get; } = new();

    // Collectible guids live well above the NPC range (NpcGuidBase 100000000000 + id) to avoid collision.
    private const ulong CollectibleGuidBase = 700000000000UL;
    private ulong _nextCollectibleGuid = CollectibleGuidBase;

    public QuestDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool TryGet(int questId, out QuestDefinition definition) => Quests.TryGetValue(questId, out definition!);

    public bool Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Quest file not found: \"{file}\". No quests will be loaded.", filePath);
            return true;
        }

        try
        {
            using var fileStream = File.OpenRead(filePath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var quests = JsonSerializer.Deserialize<List<QuestDefinition>>(fileStream, options);

            if (quests is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            foreach (var quest in quests)
            {
                if (!Quests.TryAdd(quest.QuestId, quest))
                {
                    _logger.LogWarning("Duplicate quest id {id} in \"{file}\".", quest.QuestId, filePath);
                    continue;
                }

                if (quest.GiverGuid != 0)
                    ByGiver.GetOrAdd(quest.GiverGuid, _ => new List<int>()).Add(quest.QuestId);

                if (quest.TargetGuid != 0)
                    ByTarget.GetOrAdd(quest.TargetGuid, _ => new List<int>()).Add(quest.QuestId);

                // Index every goal's target NPC too, so multi-goal quests can point intermediate goals at
                // NPCs that aren't the giver/turn-in - otherwise those NPCs wouldn't get a quest interaction
                // (IsQuestNpc gates the interact action at spawn on ByGiver/ByTarget).
                var goalNameIds = new HashSet<int>();
                foreach (var goal in quest.EffectiveGoals)
                {
                    if (goal.TargetGuid != 0 && goal.TargetGuid != quest.TargetGuid
                        && !ByTarget.GetOrAdd(goal.TargetGuid, _ => new List<int>()).Contains(quest.QuestId))
                        ByTarget[goal.TargetGuid].Add(quest.QuestId);

                    // Goal NameIds double as the client's objective identity (QuestObjectiveAdded body
                    // int0 -> row hash key) - a duplicate makes goals indistinguishable client-side.
                    if (!goalNameIds.Add(goal.NameId))
                        _logger.LogWarning("Quest {id}: duplicate goal NameId {nameId} - goals will collide client-side (checkmarks/advance won't render correctly).", quest.QuestId, goal.NameId);
                }

                // Assign world guids to each Collect goal's pickups and index them back to (quest, goal), so
                // interacting with a pickup credits the right goal. Goals index matches EffectiveGoals.
                var effective = quest.EffectiveGoals;
                for (int gi = 0; gi < effective.Count; gi++)
                {
                    var goal = effective[gi];

                    // Kill goals: remember every counted NPC NameId (see KillTargetNameIds).
                    if (goal.Type == QuestGoalType.Kill)
                        foreach (var killNameId in goal.AllKillNameIds())
                            KillTargetNameIds.Add(killNameId);

                    if (goal.Type != QuestGoalType.Collect)
                        continue;

                    // Default the required count to "collect them all" so the tracker's 0/N matches the pickups.
                    if (goal.RequiredCount <= 0)
                        goal.RequiredCount = goal.CollectSpawns.Count;

                    foreach (var pos in goal.CollectSpawns)
                    {
                        if (pos is null || pos.Length < 3)
                            continue;

                        var guid = _nextCollectibleGuid++;
                        Collectibles[guid] = (quest.QuestId, gi);
                        CollectibleSpawns.Add(new CollectibleSpawn
                        {
                            Guid = guid,
                            ModelId = goal.CollectModelId,
                            NameId = goal.CollectNameId,
                            Position = new Vector4(pos[0], pos[1], pos[2], 1f)
                        });
                    }
                }
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
}
