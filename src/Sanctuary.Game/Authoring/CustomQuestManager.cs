using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.QuestGoals;

namespace Sanctuary.Game.Authoring;

public interface ICustomQuestManager
{
    /// <summary>
    /// Publishes every saved quest marked published. Call after resources load.
    /// </summary>
    void Load();

    IReadOnlyList<DbCustomQuest> GetAll();

    bool TryGet(int id, out DbCustomQuest quest);

    DbCustomQuest Create(string title, ulong? createdBy);

    /// <summary>
    /// Applies an edit and saves; a published quest is rebuilt so the change is live.
    /// Returns the error to show the GM, or null.
    /// </summary>
    string? Update(int id, ulong? updatedBy, Func<DbCustomQuest, string?> edit);

    string? Publish(int id, ulong? updatedBy);

    bool Unpublish(int id, ulong? updatedBy);

    bool Delete(int id);

    /// <summary>
    /// Sends the free text for a custom quest's goal when it starts (goal 0 = also the quest intro).
    /// </summary>
    void AnnounceGoal(Player player, int questId, int goalIndex);

    void AnnounceComplete(Player player, int questId);
}

/// <summary>
/// GM-authored quests, built on the regular quest system: each published quest becomes a
/// <see cref="QuestDefinition"/> with id <see cref="QuestIdBase"/> + its row id.
/// </summary>
/// <remarks>
/// The client's quest window only shows locale strings, so custom quests display generic ones there
/// (or a GM-chosen string id), and their free text is sent to the player in chat instead.
/// </remarks>
public class CustomQuestManager : ICustomQuestManager
{
    public const int QuestIdBase = 900000;
    public const int MaxGoals = 8;
    public const int MaxRewards = 8;

    // Generic locale strings for the client's quest window.
    private const int GenericTitleId = 79;          // "Quest"
    private const int GenericDescriptionId = 8125;  // "Can you help me?"
    private const int ObjectivesHeaderId = 914;     // "Objectives"
    private const int TurnInLineId = 35;            // "Thank you!"

    /// <summary>
    /// Distinct objective row labels, one per goal: progress is keyed by goal NameId.
    /// </summary>
    private static readonly int[] GoalNameIds = [3300, 429935, 433081, 914, 16375, 79, 671, 3486];

    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly object _lock = new();

    public CustomQuestManager(ILogger<CustomQuestManager> logger, IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
    }

    public static bool IsCustomQuestId(int questId) => questId > QuestIdBase;

    public void Load()
    {
        var published = 0;

        foreach (var quest in GetAll().Where(x => x.IsPublished))
        {
            if (Validate(quest) is { } error)
            {
                _logger.LogWarning("Custom quest {Id} was not published: {Error}", quest.Id, error);
                continue;
            }

            _resourceManager.Quests.AddOrReplace(BuildDefinition(quest), $"custom quest {quest.Id}");
            published++;
        }

        _logger.LogInformation("Published {Count} custom quest(s).", published);
    }

    public IReadOnlyList<DbCustomQuest> GetAll()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.CustomQuests.AsNoTracking().OrderBy(x => x.Id).ToList();
    }

    public bool TryGet(int id, out DbCustomQuest quest)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        quest = dbContext.CustomQuests.AsNoTracking().SingleOrDefault(x => x.Id == id)!;

        return quest is not null;
    }

    public DbCustomQuest Create(string title, ulong? createdBy)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var quest = new DbCustomQuest { Title = Truncate(title, 64), UpdatedBy = createdBy };

        dbContext.CustomQuests.Add(quest);
        dbContext.SaveChanges();

        return quest;
    }

    public string? Update(int id, ulong? updatedBy, Func<DbCustomQuest, string?> edit)
    {
        lock (_lock)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var quest = dbContext.CustomQuests.SingleOrDefault(x => x.Id == id);

            if (quest is null)
                return $"No custom quest {id}.";

            if (edit(quest) is { } error)
                return error;

            // Keep a live quest valid; edits that would break it have to unpublish first.
            if (quest.IsPublished && Validate(quest) is { } publishedError)
                return $"{publishedError} Unpublish the quest to make that change.";

            quest.UpdatedBy = updatedBy;
            quest.Updated = DateTimeOffset.UtcNow;
            dbContext.SaveChanges();

            if (quest.IsPublished)
                _resourceManager.Quests.AddOrReplace(BuildDefinition(quest), $"custom quest {quest.Id}");

            return null;
        }
    }

    public string? Publish(int id, ulong? updatedBy)
    {
        lock (_lock)
        {
            if (!TryGet(id, out var quest))
                return $"No custom quest {id}.";

            if (Validate(quest) is { } error)
                return error;

            var updateError = Update(id, updatedBy, q =>
            {
                q.IsPublished = true;
                return null;
            });

            // Update rebuilds and adds the definition now that it's published.
            return updateError;
        }
    }

    public bool Unpublish(int id, ulong? updatedBy)
    {
        lock (_lock)
        {
            var error = Update(id, updatedBy, q =>
            {
                q.IsPublished = false;
                return null;
            });

            if (error is not null)
                return false;

            _resourceManager.Quests.Remove(QuestIdBase + id);

            return true;
        }
    }

    public bool Delete(int id)
    {
        lock (_lock)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            if (dbContext.CustomQuests.Where(x => x.Id == id).ExecuteDelete() == 0)
                return false;

            _resourceManager.Quests.Remove(QuestIdBase + id);

            return true;
        }
    }

    /// <summary>
    /// What stops a quest from being published, or null.
    /// </summary>
    private string? Validate(DbCustomQuest quest)
    {
        var goals = CustomQuestJson.ReadList<CustomQuestGoal>(quest.Goals);

        if (quest.GiverNpcId == 0)
            return "Set a giver NPC first.";

        if (goals.Count == 0)
            return "Add at least one goal first.";

        for (var i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];

            switch (goal.Type)
            {
                case CustomQuestGoal.Talk when goal.NpcId == 0:
                    return $"Goal {i} has no NPC to talk to.";

                case CustomQuestGoal.Collect when string.IsNullOrEmpty(goal.NodeType) || !_resourceManager.CollectionNodeTypes.ContainsKey(goal.NodeType):
                    return $"Goal {i} collects unknown node type '{goal.NodeType}'. Use !questedit nodes.";

                case CustomQuestGoal.Collect when goal.Count < 1:
                    return $"Goal {i} needs a count of at least 1.";

                case CustomQuestGoal.Reach when goal.X == 0 && goal.Z == 0:
                    return $"Goal {i} has no location.";
            }
        }

        return null;
    }

    private static QuestDefinition BuildDefinition(DbCustomQuest quest)
    {
        var goals = CustomQuestJson.ReadList<CustomQuestGoal>(quest.Goals);
        var rewards = CustomQuestJson.ReadList<CustomQuestReward>(quest.Rewards);

        var definition = new QuestDefinition
        {
            QuestId = QuestIdBase + quest.Id,
            TitleId = quest.TitleId != 0 ? quest.TitleId : GenericTitleId,
            DescriptionId = quest.DescriptionId != 0 ? quest.DescriptionId : GenericDescriptionId,
            GiverDialogueId = quest.DescriptionId != 0 ? quest.DescriptionId : GenericDescriptionId,
            ObjectiveDescriptionId = ObjectivesHeaderId,
            GiverNpcId = quest.GiverNpcId,
            TargetNpcId = quest.TurnInNpcId != 0 ? quest.TurnInNpcId : quest.GiverNpcId,
            RewardCoins = quest.RewardCoins,
            // The quest system grants each listed item once, so a quantity repeats the id.
            RewardItems = [.. rewards.SelectMany(reward => Enumerable.Repeat(reward.ItemId, reward.Quantity))],
            PrerequisiteQuestId = quest.PrerequisiteQuestId,
            NextQuestId = quest.NextQuestId
        };

        for (var i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];

            QuestGoal questGoal = goal.Type switch
            {
                CustomQuestGoal.Collect => new CollectGoal { CollectNodeType = goal.NodeType ?? string.Empty, RequiredCount = goal.Count },
                CustomQuestGoal.Reach => new ReachLocationGoal
                {
                    ReachPosition = new Vector3(goal.X, goal.Y, goal.Z),
                    ReachRadius = goal.Radius,
                    ZoneDefinitionId = goal.ZoneId
                },
                _ => new TalkToNpcGoal { Targets = [new TalkTarget { NpcId = goal.NpcId }] }
            };

            questGoal.NameId = GoalNameIds[i];

            // The turn-in line, shown when the last goal completes.
            if (i == goals.Count - 1)
                questGoal.DialogueId = TurnInLineId;

            definition.Goals.Add(questGoal);
        }

        return definition;
    }

    #region Free text

    public void AnnounceGoal(Player player, int questId, int goalIndex)
    {
        if (!IsCustomQuestId(questId) || !TryGet(questId - QuestIdBase, out var quest))
            return;

        var goals = CustomQuestJson.ReadList<CustomQuestGoal>(quest.Goals);

        if (goalIndex == 0)
        {
            ChatHelper.SendSystemMessage(player, $"Quest: {quest.Title}");

            if (!string.IsNullOrEmpty(quest.Description))
                ChatHelper.SendSystemMessage(player, quest.Description);
        }

        if (goalIndex < goals.Count)
            ChatHelper.SendSystemMessage(player, $"Goal: {DescribeGoal(goals[goalIndex])}");
    }

    public void AnnounceComplete(Player player, int questId)
    {
        if (IsCustomQuestId(questId) && TryGet(questId - QuestIdBase, out var quest))
            ChatHelper.SendSystemMessage(player, $"Quest complete: {quest.Title}");
    }

    private string DescribeGoal(CustomQuestGoal goal)
    {
        if (!string.IsNullOrEmpty(goal.Text))
            return goal.Text;

        return goal.Type switch
        {
            CustomQuestGoal.Collect => $"Collect {goal.Count} {goal.NodeType}.",
            CustomQuestGoal.Reach => $"Go to ({goal.X:F0}, {goal.Z:F0}).",
            _ => $"Talk to {NpcName(goal.NpcId)}."
        };
    }

    private string NpcName(int npcId) =>
        _resourceManager.Npcs.TryGetValue(npcId, out var npc) && !string.IsNullOrEmpty(npc.Name) ? npc.Name : $"NPC #{npcId}";

    #endregion

    private static string Truncate(string text, int length) => text.Length > length ? text[..length] : text;
}
