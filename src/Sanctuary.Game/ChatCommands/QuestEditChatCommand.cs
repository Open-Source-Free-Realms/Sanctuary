using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Sanctuary.Database.Entities;
using Sanctuary.Game.Authoring;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Authors custom quests (see <see cref="CustomQuestManager"/>). Every edit re-sends the quest to the
/// GM panel as questedit/questgoal/questreward lines. Text arguments are escaped (see <see cref="PanelTextHelper"/>).
/// </summary>
public class QuestEditChatCommand : IRawChatCommand
{
    private readonly ICustomQuestManager _customQuestManager;
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "questedit";
    public string Usage => "list | nodes | new <title> | show <id> | set <id> <field> <value> | goal add <id> talk <npcDefId> " +
        "| goal add <id> collect <nodeType> <count> | goal add <id> reach [<zone> <x> <y> <z> [radius]] | goal text <id> <index> <text> " +
        "| goal del|up|down <id> <index> | reward add|del <id> <itemId> [qty] | publish|unpublish|delete <id>";
    public string Description => $"Authors custom quests (ids {CustomQuestManager.QuestIdBase}+). Fields: title, desc, giver, turnin, coins, prereq, next, titleid, descid.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public QuestEditChatCommand(ICustomQuestManager customQuestManager, IResourceManager resourceManager, IChatCommandManager chatCommandManager)
    {
        _customQuestManager = customQuestManager;
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args) => Handle(invoker, args, string.Join(' ', args));

    public bool Handle(Player invoker, string[] args, string rawArgs)
    {
        if (args.Length == 0)
            return false;

        var action = args[0].ToLowerInvariant();

        switch (action)
        {
            case "list":
                SendList(invoker);
                return true;

            case "nodes":
                ChatHelper.SendGmPanelData(invoker, "questnodes", [.. _resourceManager.CollectionNodeTypes.Keys.Order()]);
                ChatHelper.SendSystemMessage(invoker, $"Collect node types: {string.Join(", ", _resourceManager.CollectionNodeTypes.Keys.Order())}");
                return true;

            case "new":
                if (args.Length < 2)
                    return false;

                var created = _customQuestManager.Create(PanelTextHelper.Unescape(ChatCommandText.Remainder(rawArgs, 1)), invoker.CharacterId);

                _chatCommandManager.LogAction(this, invoker, "New", null, $"Quest: {created.Id}");
                ChatHelper.SendSystemMessage(invoker, $"Created custom quest {created.Id} (quest id {CustomQuestManager.QuestIdBase + created.Id}).");
                SendQuest(invoker, created.Id);
                return true;
        }

        string? error = null;
        int id;

        // "goal <action> <id> ..." and "reward <action> <id> ..." name their action before the id.
        if (action is "goal" or "reward")
        {
            if (args.Length < 4 || !int.TryParse(args[2], out id))
                return false;
        }
        else if (args.Length < 2 || !int.TryParse(args[1], out id))
        {
            return false;
        }

        switch (action)
        {
            case "show":
                break;

            case "set":
                if (args.Length < 4)
                    return false;

                error = _customQuestManager.Update(id, invoker.CharacterId, quest => SetField(quest, args[2].ToLowerInvariant(), ChatCommandText.Remainder(rawArgs, 3)));
                break;

            case "goal":
                error = EditGoal(invoker, id, args, rawArgs);
                break;

            case "reward":
                error = EditReward(invoker, id, args);
                break;

            case "publish":
                error = _customQuestManager.Publish(id, invoker.CharacterId);
                break;

            case "unpublish":
                error = _customQuestManager.Unpublish(id, invoker.CharacterId) ? null : $"No custom quest {id}.";
                break;

            case "delete":
                error = _customQuestManager.Delete(id) ? null : $"No custom quest {id}.";

                if (error is null)
                {
                    _chatCommandManager.LogAction(this, invoker, "Delete", null, $"Quest: {id}");
                    ChatHelper.SendSystemMessage(invoker, $"Deleted custom quest {id}.");
                    SendList(invoker);
                    return true;
                }
                break;

            default:
                return false;
        }

        if (action != "show")
        {
            if (error is not null)
            {
                ChatHelper.SendSystemMessage(invoker, $"Quest edit failed: {error}");
                return true;
            }

            _chatCommandManager.LogAction(this, invoker, action, null, $"Quest: {id}, Args: {rawArgs}");
        }

        if (!SendQuest(invoker, id))
            ChatHelper.SendSystemMessage(invoker, $"No custom quest {id}.");

        return true;
    }

    private string? SetField(DbCustomQuest quest, string field, string value)
    {
        switch (field)
        {
            case "title":
                var title = PanelTextHelper.Unescape(value);

                if (title.Length is 0 or > 64)
                    return "Titles are 1-64 characters.";

                quest.Title = title;
                return null;

            case "desc":
                var description = PanelTextHelper.Unescape(value);

                if (description.Length > 400)
                    return "Descriptions are at most 400 characters.";

                quest.Description = description.Length == 0 ? null : description;
                return null;
        }

        if (!int.TryParse(value.Trim(), out var number) || number < 0)
            return $"{field} needs a whole number.";

        switch (field)
        {
            case "giver":
            case "turnin":
                if (number != 0 && !_resourceManager.Npcs.ContainsKey(number))
                    return $"Unknown NPC definition {number}.";

                if (field == "giver")
                    quest.GiverNpcId = number;
                else
                    quest.TurnInNpcId = number;
                return null;

            case "coins":
                quest.RewardCoins = number;
                return null;

            case "prereq":
                quest.PrerequisiteQuestId = number == 0 ? null : number;
                return null;

            case "next":
                quest.NextQuestId = number == 0 ? null : number;
                return null;

            case "titleid":
                quest.TitleId = number;
                return null;

            case "descid":
                quest.DescriptionId = number;
                return null;

            default:
                return $"Unknown field '{field}'.";
        }
    }

    /// <summary>
    /// goal add|text|del|up|down ...
    /// </summary>
    private string? EditGoal(Player invoker, int id, string[] args, string rawArgs)
    {
        var sub = args[1].ToLowerInvariant();

        return _customQuestManager.Update(id, invoker.CharacterId, quest =>
        {
            var goals = CustomQuestJson.ReadList<CustomQuestGoal>(quest.Goals);

            string? result = sub switch
            {
                "add" => AddGoal(invoker, goals, args[3..]),
                "text" => SetGoalText(goals, args, rawArgs),
                "del" or "up" or "down" => MoveGoal(goals, sub, args),
                _ => "Unknown goal action."
            };

            if (result is null)
                quest.Goals = CustomQuestJson.Write(goals);

            return result;
        });
    }

    private string? AddGoal(Player invoker, List<CustomQuestGoal> goals, string[] args)
    {
        if (goals.Count >= CustomQuestManager.MaxGoals)
            return $"Quests have at most {CustomQuestManager.MaxGoals} goals.";

        if (args.Length == 0)
            return "Which goal type? talk, collect or reach.";

        var goal = new CustomQuestGoal { Type = args[0].ToLowerInvariant() };

        switch (goal.Type)
        {
            case CustomQuestGoal.Talk:
                if (args.Length != 2 || !int.TryParse(args[1], out var npcId) || !_resourceManager.Npcs.ContainsKey(npcId))
                    return "talk needs a known NPC definition id.";

                goal.NpcId = npcId;
                break;

            case CustomQuestGoal.Collect:
                if (args.Length != 3 || !_resourceManager.CollectionNodeTypes.ContainsKey(args[1]) || !int.TryParse(args[2], out var count) || count < 1)
                    return "collect needs a node type (see !questedit nodes) and a count of at least 1.";

                goal.NodeType = args[1];
                goal.Count = count;
                break;

            case CustomQuestGoal.Reach:
                if (args.Length == 1)
                {
                    goal.ZoneId = invoker.Zone.DefinitionId;
                    goal.X = invoker.Position.X;
                    goal.Y = invoker.Position.Y;
                    goal.Z = invoker.Position.Z;
                    break;
                }

                if (args.Length is < 5 or > 6
                    || !int.TryParse(args[1], out var zoneId)
                    || !TryParseFloat(args[2], out var x) || !TryParseFloat(args[3], out var y) || !TryParseFloat(args[4], out var z))
                    return "reach needs <zone> <x> <y> <z> [radius], or nothing to use where you stand.";

                var radius = 8f;

                if (args.Length == 6 && (!TryParseFloat(args[5], out radius) || radius <= 0f))
                    return "radius must be above 0.";

                (goal.ZoneId, goal.X, goal.Y, goal.Z, goal.Radius) = (zoneId, x, y, z, radius);
                break;

            default:
                return "Goal types are talk, collect and reach.";
        }

        goals.Add(goal);
        return null;
    }

    private static string? SetGoalText(List<CustomQuestGoal> goals, string[] args, string rawArgs)
    {
        // "goal text <id> <index> <text>"
        if (args.Length < 5 || !int.TryParse(args[3], out var index) || index < 0 || index >= goals.Count)
            return "goal text needs a goal index and text.";

        var text = PanelTextHelper.Unescape(ChatCommandText.Remainder(rawArgs, 4));

        if (text.Length > 200)
            return "Goal text is at most 200 characters.";

        goals[index].Text = text.Length == 0 ? null : text;
        return null;
    }

    private static string? MoveGoal(List<CustomQuestGoal> goals, string sub, string[] args)
    {
        if (args.Length != 4 || !int.TryParse(args[3], out var index) || index < 0 || index >= goals.Count)
            return "Give a valid goal index.";

        var target = sub switch { "up" => index - 1, "down" => index + 1, _ => index };

        if (sub == "del")
        {
            goals.RemoveAt(index);
            return null;
        }

        if (target < 0 || target >= goals.Count)
            return "That goal can't move further.";

        (goals[index], goals[target]) = (goals[target], goals[index]);
        return null;
    }

    private string? EditReward(Player invoker, int id, string[] args)
    {
        // "reward add|del <id> <itemId> [qty]"
        var sub = args[1].ToLowerInvariant();

        if (!int.TryParse(args[3], out var itemId))
            return "reward needs an item id.";

        var quantity = 1;

        if (args.Length > 4 && (!int.TryParse(args[4], out quantity) || quantity is < 1 or > 10))
            return "Quantity is 1-10.";

        if (sub == "add" && !_resourceManager.ClientItemDefinitions.ContainsKey(itemId))
            return $"Unknown item {itemId}.";

        return _customQuestManager.Update(id, invoker.CharacterId, quest =>
        {
            var rewards = CustomQuestJson.ReadList<CustomQuestReward>(quest.Rewards);

            switch (sub)
            {
                case "add":
                    if (rewards.Count >= CustomQuestManager.MaxRewards && rewards.All(x => x.ItemId != itemId))
                        return $"Quests have at most {CustomQuestManager.MaxRewards} reward items.";

                    rewards.RemoveAll(x => x.ItemId == itemId);
                    rewards.Add(new CustomQuestReward { ItemId = itemId, Quantity = quantity });
                    break;

                case "del":
                    if (rewards.RemoveAll(x => x.ItemId == itemId) == 0)
                        return $"Item {itemId} isn't a reward.";
                    break;

                default:
                    return "reward add or del.";
            }

            quest.Rewards = CustomQuestJson.Write(rewards);
            return null;
        });
    }

    private void SendList(Player invoker)
    {
        ChatHelper.SendGmPanelData(invoker, "questlist", "begin");

        foreach (var quest in _customQuestManager.GetAll())
        {
            ChatHelper.SendGmPanelData(invoker, "questlist", "entry", quest.Id, PanelTextHelper.Escape(quest.Title),
                quest.IsPublished ? 1 : 0, CustomQuestJson.ReadList<CustomQuestGoal>(quest.Goals).Count);
        }

        ChatHelper.SendGmPanelData(invoker, "questlist", "end");
    }

    private bool SendQuest(Player invoker, int id)
    {
        if (!_customQuestManager.TryGet(id, out var quest))
            return false;

        // id, title, description, giver, turn-in, coins, prerequisite, next, published, title id, description id
        ChatHelper.SendGmPanelData(invoker, "questedit", "begin", quest.Id, PanelTextHelper.Escape(quest.Title),
            PanelTextHelper.Escape(quest.Description ?? string.Empty), quest.GiverNpcId, quest.TurnInNpcId, quest.RewardCoins,
            quest.PrerequisiteQuestId ?? 0, quest.NextQuestId ?? 0, quest.IsPublished ? 1 : 0, quest.TitleId, quest.DescriptionId);

        var goals = CustomQuestJson.ReadList<CustomQuestGoal>(quest.Goals);

        for (var i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];

            // index, type, a, b, c, d, e, text
            object[] fields = goal.Type switch
            {
                CustomQuestGoal.Collect => [goal.NodeType ?? string.Empty, goal.Count, "", "", ""],
                CustomQuestGoal.Reach => [goal.ZoneId, Format(goal.X), Format(goal.Y), Format(goal.Z), Format(goal.Radius)],
                _ => [goal.NpcId, "", "", "", ""]
            };

            ChatHelper.SendGmPanelData(invoker, "questgoal", [i, goal.Type, .. fields, PanelTextHelper.Escape(goal.Text ?? string.Empty)]);
        }

        foreach (var reward in CustomQuestJson.ReadList<CustomQuestReward>(quest.Rewards))
            ChatHelper.SendGmPanelData(invoker, "questreward", reward.ItemId, reward.Quantity);

        ChatHelper.SendGmPanelData(invoker, "questedit", "end");

        return true;
    }

    private static bool TryParseFloat(string value, out float result) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
