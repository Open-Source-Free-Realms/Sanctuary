using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.ChatCommands;

public class QuestChatCommand : IChatCommand
{
    private const int FindLimit = 15;

    private readonly IChatCommandManager _chatCommandManager;
    private readonly IQuestManager _questManager;
    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "quest";

    public string Usage => "list [player] | info <questId> | find <text> | start|skip|finish|reset <questId> [player]";

    public string Description => "Referee quest tools: inspect quests and a player's quest log, start a quest, skip its current goal, finish it, or reset it so it can be taken again.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public QuestChatCommand(IChatCommandManager chatCommandManager, IQuestManager questManager, IZoneManager zoneManager,
        IResourceManager resourceManager)
    {
        _chatCommandManager = chatCommandManager;
        _questManager = questManager;
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        var subCommand = args[0].ToLowerInvariant();

        switch (subCommand)
        {
            case "list":
                return HandleList(invoker, args[1..]);

            case "find":
                return HandleFind(invoker, args[1..]);
        }

        if (args.Length < 2 || !int.TryParse(args[1], out var questId))
            return false;

        if (!_resourceManager.Quests.TryGet(questId, out var quest))
        {
            ChatHelper.SendSystemMessage(invoker, $"Quest {questId} does not exist.");
            return true;
        }

        if (subCommand == "info")
            return HandleInfo(invoker, quest);

        if (!TryGetTarget(invoker, args[2..], out var target, out var targetName))
            return true;

        return subCommand switch
        {
            "start" => HandleStart(invoker, target, targetName, quest),
            "skip" => HandleSkip(invoker, target, targetName, quest),
            "finish" => HandleFinish(invoker, target, targetName, quest),
            "reset" => HandleReset(invoker, target, targetName, quest),
            _ => false
        };
    }

    private bool TryGetTarget(Player invoker, string[] args, out Player target, out string? targetName)
    {
        targetName = args.Length > 0 ? string.Join(' ', args) : null;
        target = invoker;

        if (targetName is not null && !_zoneManager.TryGetPlayer(targetName, out target))
        {
            ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
            return false;
        }

        return true;
    }

    private static string GetTitle(QuestDefinition quest) => GameNameHelper.Get(quest.TitleId, $"Quest {quest.QuestId}");

    private bool HandleList(Player invoker, string[] args)
    {
        if (!TryGetTarget(invoker, args, out var target, out _))
            return true;

        var inProgress = target.Quests.InProgress;

        ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName}: {inProgress.Count} in progress, {target.Quests.CompletedCount} completed, tracking {target.Quests.ActiveQuestId}.");

        foreach (var progress in inProgress)
        {
            if (!_resourceManager.Quests.TryGet(progress.QuestId, out var quest))
                continue;

            var goal = progress.GoalIndex < quest.Goals.Count ? $"goal {progress.GoalIndex + 1}/{quest.Goals.Count}" : "ready to turn in";
            ChatHelper.SendSystemMessage(invoker, $"  {quest.QuestId} {GetTitle(quest)} - {goal}");
        }

        return true;
    }

    private bool HandleFind(Player invoker, string[] args)
    {
        var search = string.Join(' ', args).Trim();

        if (search.Length == 0)
            return false;

        var matches = _resourceManager.Quests.Quests.Values
            .Where(quest => quest.QuestId.ToString() == search || GetTitle(quest).Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(quest => quest.QuestId)
            .ToList();

        ChatHelper.SendSystemMessage(invoker, $"{matches.Count} quest(s) match \"{search}\".");

        foreach (var quest in matches.Take(FindLimit))
            ChatHelper.SendSystemMessage(invoker, $"  {quest.QuestId} {GetTitle(quest)}");

        return true;
    }

    private bool HandleInfo(Player invoker, QuestDefinition quest)
    {
        ChatHelper.SendSystemMessage(invoker, $"{quest.QuestId} {GetTitle(quest)}: {quest.Goals.Count} goal(s), giver NPC {quest.GiverNpcId}, turn-in NPC {quest.TargetNpcId}.");
        ChatHelper.SendSystemMessage(invoker, $"  Requires {quest.PrerequisiteQuestId?.ToString() ?? "nothing"}, leads to {quest.NextQuestId?.ToString() ?? "nothing"}. Rewards {quest.RewardCoins} coins, items [{string.Join(", ", quest.RewardItems)}].");

        for (var i = 0; i < quest.Goals.Count; i++)
            ChatHelper.SendSystemMessage(invoker, $"  {i + 1}. {quest.Goals[i].GetType().Name} {GameNameHelper.Get(quest.Goals[i].NameId, quest.Goals[i].NameId.ToString())}");

        return true;
    }

    private bool HandleStart(Player invoker, Player target, string? targetName, QuestDefinition quest)
    {
        if (target.Quests.Contains(quest.QuestId))
        {
            ChatHelper.SendSystemMessage(invoker, $"Quest {quest.QuestId} is already in that player's log. Reset it first to start it again.");
            return true;
        }

        _questManager.AcceptQuest(target, quest.QuestId);

        if (!target.Quests.Contains(quest.QuestId))
        {
            ChatHelper.SendSystemMessage(invoker, $"Quest {quest.QuestId} can't be offered to that player (missing prerequisite or excluded by another quest).");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Start quest", targetName, quest.QuestId.ToString());
        ChatHelper.SendSystemMessage(invoker, $"Started {GetTitle(quest)}.");
        return true;
    }

    private bool HandleSkip(Player invoker, Player target, string? targetName, QuestDefinition quest)
    {
        if (!_questManager.SkipGoal(target, quest.QuestId))
        {
            ChatHelper.SendSystemMessage(invoker, $"Quest {quest.QuestId} isn't in progress with a goal left to skip.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Skip quest goal", targetName, quest.QuestId.ToString());
        ChatHelper.SendSystemMessage(invoker, $"Skipped the current goal of {GetTitle(quest)}.");
        return true;
    }

    private bool HandleFinish(Player invoker, Player target, string? targetName, QuestDefinition quest)
    {
        if (!target.Quests.TryGetInProgress(quest.QuestId, out _))
        {
            ChatHelper.SendSystemMessage(invoker, $"Quest {quest.QuestId} isn't in progress for that player.");
            return true;
        }

        while (_questManager.SkipGoal(target, quest.QuestId))
        {
        }

        if (!_questManager.CompleteQuest(target, quest.QuestId))
        {
            ChatHelper.SendSystemMessage(invoker, $"Couldn't complete quest {quest.QuestId}.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Finish quest", targetName, quest.QuestId.ToString());
        ChatHelper.SendSystemMessage(invoker, $"Finished {GetTitle(quest)} and granted its rewards.");
        return true;
    }

    private bool HandleReset(Player invoker, Player target, string? targetName, QuestDefinition quest)
    {
        if (!_questManager.ResetQuest(target, quest.QuestId))
        {
            ChatHelper.SendSystemMessage(invoker, $"Quest {quest.QuestId} is not in that player's log.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Reset quest", targetName, quest.QuestId.ToString());
        ChatHelper.SendSystemMessage(invoker, $"Reset quest {quest.QuestId}.");
        return true;
    }
}
