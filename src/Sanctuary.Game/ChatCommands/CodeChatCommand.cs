using System;
using System.Globalization;

using Sanctuary.Database.Entities;
using Sanctuary.Game.Authoring;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Creates and configures redeem codes (see <see cref="RedeemCodeManager"/>). Lists and the editor go to
/// the GM panel as codes/codeedit lines.
/// </summary>
public class CodeChatCommand : IRawChatCommand
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IRedeemCodeManager _redeemCodeManager;
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "code";
    public string Usage => "list | new [code|random] | show <code> | set <code> <field> <value> | item add|del <code> <itemId> [qty] | delete <code>";
    public string Description => "Makes redeem codes. Fields: coins, uses (0 = unlimited), percharacter (0 = unlimited), starts, expires (yyyy-MM-dd, +<days> or never), members on|off, enabled on|off, note.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public CodeChatCommand(IRedeemCodeManager redeemCodeManager, IResourceManager resourceManager, IChatCommandManager chatCommandManager)
    {
        _redeemCodeManager = redeemCodeManager;
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args) => Handle(invoker, args, string.Join(' ', args));

    public bool Handle(Player invoker, string[] args, string rawArgs)
    {
        if (args.Length == 0)
            return false;

        var action = args[0].ToLowerInvariant();
        string code;
        string? error;

        switch (action)
        {
            case "list":
                SendList(invoker);
                return true;

            case "new":
                var created = _redeemCodeManager.Create(args.Length > 1 ? args[1] : null, invoker.CharacterId, out var createError);

                if (created is null)
                {
                    ChatHelper.SendSystemMessage(invoker, $"Code failed: {createError}");
                    return true;
                }

                _chatCommandManager.LogAction(this, invoker, "New", null, $"Code: {created.Code}");
                ChatHelper.SendSystemMessage(invoker, $"Created code {created.Code}. Add rewards with !code item add {created.Code} <itemId> or !code set {created.Code} coins <n>.");
                SendCode(invoker, created.Code);
                return true;

            case "show":
                if (args.Length != 2)
                    return false;

                code = args[1];
                error = null;
                break;

            case "set":
                if (args.Length < 4)
                    return false;

                code = args[1];
                var field = args[2].ToLowerInvariant();
                var value = PanelTextHelper.Unescape(ChatCommandText.Remainder(rawArgs, 3)).Trim();

                error = _redeemCodeManager.Update(code, redeemCode => SetField(redeemCode, field, value));
                break;

            case "item":
                // "item add|del <code> <itemId> [qty]"
                if (args.Length < 4 || !int.TryParse(args[3], out var itemId))
                    return false;

                code = args[2];
                var quantity = 1;

                if (args.Length > 4 && (!int.TryParse(args[4], out quantity) || quantity is < 1 or > 999))
                    return false;

                error = EditItems(code, args[1].ToLowerInvariant(), itemId, quantity);
                break;

            case "delete":
                if (args.Length != 2)
                    return false;

                if (!_redeemCodeManager.Delete(args[1]))
                {
                    ChatHelper.SendSystemMessage(invoker, $"No code {RedeemCodeManager.Normalize(args[1])}.");
                    return true;
                }

                _chatCommandManager.LogAction(this, invoker, "Delete", null, $"Code: {RedeemCodeManager.Normalize(args[1])}");
                SendList(invoker);
                return true;

            default:
                return false;
        }

        if (error is not null)
        {
            ChatHelper.SendSystemMessage(invoker, $"Code failed: {error}");
            return true;
        }

        if (action != "show")
            _chatCommandManager.LogAction(this, invoker, action, null, $"Code: {RedeemCodeManager.Normalize(code)}, Args: {rawArgs}");

        if (!SendCode(invoker, code))
            ChatHelper.SendSystemMessage(invoker, $"No code {RedeemCodeManager.Normalize(code)}.");

        return true;
    }

    private static string? SetField(DbRedeemCode redeemCode, string field, string value)
    {
        switch (field)
        {
            case "note":
                if (value.Length > 200)
                    return "Notes are at most 200 characters.";

                redeemCode.Note = value.Length == 0 ? null : value;
                return null;

            case "members":
            case "enabled":
                if (value is not ("on" or "off"))
                    return $"{field} is on or off.";

                if (field == "members")
                    redeemCode.MembersOnly = value == "on";
                else
                    redeemCode.Enabled = value == "on";
                return null;

            case "starts":
            case "expires":
                if (!TryParseDate(value, out var date))
                    return $"{field} takes yyyy-MM-dd (UTC), +<days>, or never.";

                if (field == "starts")
                    redeemCode.StartsAt = date;
                else
                    redeemCode.ExpiresAt = date;
                return null;
        }

        if (!int.TryParse(value, out var number) || number < 0)
            return $"{field} needs a whole number of 0 or more.";

        switch (field)
        {
            case "coins": redeemCode.Coins = number; return null;
            case "uses": redeemCode.MaxUses = number; return null;
            case "percharacter": redeemCode.PerCharacterLimit = number; return null;
            default: return $"Unknown field '{field}'.";
        }
    }

    private static bool TryParseDate(string value, out DateTimeOffset? date)
    {
        date = null;

        if (value.Equals("never", StringComparison.OrdinalIgnoreCase))
            return true;

        if (value.StartsWith('+') && int.TryParse(value[1..], out var days) && days >= 0)
        {
            date = DateTimeOffset.UtcNow.AddDays(days);
            return true;
        }

        if (DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            date = new DateTimeOffset(parsed, TimeSpan.Zero);
            return true;
        }

        return false;
    }

    private string? EditItems(string code, string sub, int itemId, int quantity)
    {
        if (sub == "add" && !_resourceManager.ClientItemDefinitions.ContainsKey(itemId))
            return $"Unknown item {itemId}.";

        return _redeemCodeManager.Update(code, redeemCode =>
        {
            var items = CodeItem.Read(redeemCode.Items);

            switch (sub)
            {
                case "add":
                    if (items.Count >= RedeemCodeManager.MaxItems && items.TrueForAll(x => x.ItemId != itemId))
                        return $"Codes give at most {RedeemCodeManager.MaxItems} different items.";

                    items.RemoveAll(x => x.ItemId == itemId);
                    items.Add(new CodeItem { ItemId = itemId, Quantity = quantity });
                    break;

                case "del":
                    if (items.RemoveAll(x => x.ItemId == itemId) == 0)
                        return $"Item {itemId} isn't a reward of this code.";
                    break;

                default:
                    return "item add or del.";
            }

            redeemCode.Items = CodeItem.Write(items);
            return null;
        });
    }

    private void SendList(Player invoker)
    {
        ChatHelper.SendGmPanelData(invoker, "codes", "begin");

        foreach (var redeemCode in _redeemCodeManager.GetAll())
        {
            // entry: code, enabled, uses, max uses, expires, note
            ChatHelper.SendGmPanelData(invoker, "codes", "entry", redeemCode.Code, redeemCode.Enabled ? 1 : 0, redeemCode.Uses,
                redeemCode.MaxUses, FormatDate(redeemCode.ExpiresAt), PanelTextHelper.Escape(redeemCode.Note ?? string.Empty));
        }

        ChatHelper.SendGmPanelData(invoker, "codes", "end");
    }

    private bool SendCode(Player invoker, string code)
    {
        if (!_redeemCodeManager.TryGet(code, out var redeemCode))
            return false;

        // code, coins, max uses, uses, per character, starts, expires, members, enabled, note
        ChatHelper.SendGmPanelData(invoker, "codeedit", "begin", redeemCode.Code, redeemCode.Coins, redeemCode.MaxUses, redeemCode.Uses,
            redeemCode.PerCharacterLimit, FormatDate(redeemCode.StartsAt), FormatDate(redeemCode.ExpiresAt),
            redeemCode.MembersOnly ? 1 : 0, redeemCode.Enabled ? 1 : 0, PanelTextHelper.Escape(redeemCode.Note ?? string.Empty));

        foreach (var item in CodeItem.Read(redeemCode.Items))
            ChatHelper.SendGmPanelData(invoker, "codeitem", item.ItemId, item.Quantity);

        ChatHelper.SendGmPanelData(invoker, "codeedit", "end");

        return true;
    }

    private static string FormatDate(DateTimeOffset? date) => date?.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture) ?? string.Empty;
}
