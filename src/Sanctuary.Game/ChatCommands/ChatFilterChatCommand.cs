using System.Linq;
using System.Text;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources;

namespace Sanctuary.Game.ChatCommands;

// !chatfilter, lets admins edit the chat filter in game
public class ChatFilterChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "chatfilter";

    public string Usage => "blacklist|whitelist add <key> <regex> | remove <key> | on | off | list, or test <message>";

    public string Description => "Edit the chat filter.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public ChatFilterChatCommand(IChatCommandManager chatCommandManager, IResourceManager resourceManager)
    {
        _chatCommandManager = chatCommandManager;
        _resourceManager = resourceManager;
    }

    // routes "test" or "<list> <action> [key] [pattern]" to the right handler
    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length < 2)
            return false;

        // test runs a message through the filter without sending it
        if (args[0].ToLowerInvariant() == "test")
        {
            Test(invoker, string.Join(' ', args[1..]));
            return true;
        }

        // pick the list
        ChatFilterCollection? list = args[0].ToLowerInvariant() switch
        {
            "blacklist" => _resourceManager.ChatBlacklist,
            "whitelist" => _resourceManager.ChatWhitelist,
            _ => null
        };

        if (list is null)
            return false;

        var listName = args[0].ToLowerInvariant();

        // add, remove, on, off or list
        var action = args[1].ToLowerInvariant();

        // key is one word, everything after it is the pattern so patterns can contain spaces
        var key = args.Length > 2 ? args[2] : string.Empty;
        var pattern = args.Length > 3 ? string.Join(' ', args[3..]) : string.Empty;

        bool success;
        string message;

        switch (action)
        {
            case "add" when pattern.Length > 0:
                success = list.AddEntry(key, pattern, out message);
                break;

            case "remove" when key.Length > 0:
                success = list.RemoveEntry(key, out message);
                break;

            case "on":
            case "off":
                success = list.SetEnabled(action == "on", out message);
                break;

            case "list":
                ChatHelper.SendSystemMessage(invoker, FormatList(listName, list.Current));
                return true;

            default:
                return false;
        }

        // record successful changes in the audit log
        if (success)
            _chatCommandManager.LogAction(this, invoker, $"{listName} {action}", null, key.Length > 0 ? $"Key: \"{key}\", Pattern: \"{pattern}\"" : null);

        ChatHelper.SendSystemMessage(invoker, $"Chat {listName}: {message}");
        return true;
    }

    // shows what a message would look like after filtering
    private void Test(Player invoker, string message)
    {
        var result = ChatFilter.Censor(message, _resourceManager.ChatBlacklist.Current, _resourceManager.ChatWhitelist.Current);

        ChatHelper.SendSystemMessage(invoker, result == message ? $"Not filtered: {result}" : $"Filtered: {result}");
    }

    // builds the "list" output: on/off state and each key, pattern and note
    private static string FormatList(string listName, ChatFilterList list)
    {
        // header line, then one line per entry sorted by key
        var builder = new StringBuilder($"Chat {listName} is {(list.Enabled ? "on" : "off")}, {list.Entries.Count} entries");

        foreach (var entry in list.Entries.OrderBy(x => x.Key))
        {
            builder.Append('\n').Append(entry.Key).Append(": ").Append(entry.Pattern);

            if (!string.IsNullOrWhiteSpace(entry.Note))
                builder.Append(" - ").Append(entry.Note);
        }

        return builder.ToString();
    }
}
