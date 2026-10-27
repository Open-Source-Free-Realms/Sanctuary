using System;
using System.Linq;

using Sanctuary.Database.Entities;
using Sanctuary.Game.Authoring;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Makes new items from existing ones (see <see cref="CustomItemManager"/>). Lists and the editor go to
/// the GM panel as itemgen/itemedit lines.
/// </summary>
public class ItemGenChatCommand : IRawChatCommand
{
    private readonly ICustomItemManager _customItemManager;
    private readonly IRewardManager _rewardManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "itemgen";
    public string Usage => "list [search] | new <baseItemId> [label] | show <id> | set <id> <field> <value|clear> | give <id> [qty] | delete <id>";
    public string Description => $"Makes new items (ids {CustomItemManager.ItemIdBase}+) from existing ones. Fields: label, {string.Join(", ", CustomItemManager.Fields)}.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public ItemGenChatCommand(ICustomItemManager customItemManager, IRewardManager rewardManager, IChatCommandManager chatCommandManager)
    {
        _customItemManager = customItemManager;
        _rewardManager = rewardManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args) => Handle(invoker, args, string.Join(' ', args));

    public bool Handle(Player invoker, string[] args, string rawArgs)
    {
        if (args.Length == 0)
            return false;

        var action = args[0].ToLowerInvariant();

        if (action == "list")
        {
            SendList(invoker, args.Length > 1 ? ChatCommandText.Remainder(rawArgs, 1) : null);
            return true;
        }

        if (args.Length < 2 || !int.TryParse(args[1], out var number))
            return false;

        switch (action)
        {
            case "new":
                var label = args.Length > 2 ? PanelTextHelper.Unescape(ChatCommandText.Remainder(rawArgs, 2)) : string.Empty;
                var created = _customItemManager.Create(number, label, invoker.CharacterId, out var createError);

                if (created is null)
                {
                    ChatHelper.SendSystemMessage(invoker, $"Item gen failed: {createError}");
                    return true;
                }

                _chatCommandManager.LogAction(this, invoker, "New", null, $"Item: {CustomItemManager.ItemIdBase + created.Id}, Base: {number}");
                ChatHelper.SendSystemMessage(invoker, $"Created item {CustomItemManager.ItemIdBase + created.Id} from {number}.");
                SendItem(invoker, created.Id);
                return true;

            case "show":
                if (!SendItem(invoker, number))
                    ChatHelper.SendSystemMessage(invoker, $"No custom item {number}.");
                return true;

            case "set":
                if (args.Length < 4)
                    return false;

                var field = args[2].ToLowerInvariant();
                var value = PanelTextHelper.Unescape(ChatCommandText.Remainder(rawArgs, 3)).Trim();

                if (_customItemManager.Set(number, field, value, invoker.CharacterId) is { } setError)
                {
                    ChatHelper.SendSystemMessage(invoker, $"Item gen failed: {setError}");
                    return true;
                }

                _chatCommandManager.LogAction(this, invoker, "Set", null, $"Item: {number}, {field}={value}");
                SendItem(invoker, number);
                return true;

            case "give":
                var quantity = 1;

                if (args.Length > 2 && (!int.TryParse(args[2], out quantity) || quantity is < 1 or > 999))
                    return false;

                if (!_customItemManager.TryGet(number, out _)
                    || !_rewardManager.TryGrantItem(invoker, CustomItemManager.ItemIdBase + number, 0, quantity))
                {
                    ChatHelper.SendSystemMessage(invoker, $"Couldn't give custom item {number}.");
                    return true;
                }

                _chatCommandManager.LogAction(this, invoker, "Give", null, $"Item: {CustomItemManager.ItemIdBase + number} x{quantity}");
                return true;

            case "delete":
                if (!_customItemManager.TryDelete(number, out var deleteError))
                {
                    ChatHelper.SendSystemMessage(invoker, $"Item gen failed: {deleteError}");
                    return true;
                }

                _chatCommandManager.LogAction(this, invoker, "Delete", null, $"Item: {number}");
                ChatHelper.SendSystemMessage(invoker, $"Deleted custom item {number}.");
                SendList(invoker, null);
                return true;

            default:
                return false;
        }
    }

    private void SendList(Player invoker, string? search)
    {
        ChatHelper.SendGmPanelData(invoker, "itemgen", "begin");

        foreach (var item in _customItemManager.GetAll(search))
        {
            var fields = _customItemManager.DescribeFields(item);

            // entry: custom id, item id, label, base item id, icon image set
            ChatHelper.SendGmPanelData(invoker, "itemgen", "entry", item.Id, CustomItemManager.ItemIdBase + item.Id,
                PanelTextHelper.Escape(item.Label), item.BaseItemId, fields.First(x => x.Field == "icon").Value);
        }

        ChatHelper.SendGmPanelData(invoker, "itemgen", "end");
    }

    private bool SendItem(Player invoker, int id)
    {
        if (!_customItemManager.TryGet(id, out DbCustomItem item))
            return false;

        ChatHelper.SendGmPanelData(invoker, "itemedit", "begin", item.Id, CustomItemManager.ItemIdBase + item.Id,
            PanelTextHelper.Escape(item.Label), item.BaseItemId);

        foreach (var (field, value, overridden) in _customItemManager.DescribeFields(item))
            ChatHelper.SendGmPanelData(invoker, "itemfield", field, PanelTextHelper.Escape(value), overridden ? 1 : 0);

        ChatHelper.SendGmPanelData(invoker, "itemedit", "end");

        return true;
    }
}
