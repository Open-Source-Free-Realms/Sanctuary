using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Shared logic for !fte and !tutorial, which manage a player's seen first-time events and minigame tutorials.
/// </summary>
public abstract class TutorialSetChatCommand : IChatCommand
{
    private const int PageSize = 15;

    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    protected abstract TutorialSet Set { get; }

    public abstract string KeyWord { get; }
    public string Usage => "list [page] | seen [player] | mark <id|name|all> [player] | unmark <id|name> [player] | reset [player]";
    public abstract string Description { get; }
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    protected TutorialSetChatCommand(IZoneManager zoneManager, IResourceManager resourceManager, IChatCommandManager chatCommandManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
        _dbContextFactory = dbContextFactory;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "list":
                return HandleList(invoker, args[1..]);

            case "seen":
                return HandleSeen(invoker, args[1..]);

            case "mark":
            case "unmark":
                return HandleMark(invoker, args[1..], args[0].Equals("mark", StringComparison.OrdinalIgnoreCase));

            case "reset":
                return HandleReset(invoker, args[1..]);

            default:
                return false;
        }
    }

    private bool HandleList(Player invoker, string[] args)
    {
        var page = 1;

        if (args.Length > 0 && (!int.TryParse(args[0], out page) || page < 1))
            return false;

        var definitions = TutorialHelper.GetDefinitions(_resourceManager, Set).OrderBy(x => x.Key).ToList();
        var seen = TutorialHelper.GetSet(invoker, Set);
        var pageCount = Math.Max(1, (definitions.Count + PageSize - 1) / PageSize);

        ChatHelper.SendSystemMessage(invoker, $"{KeyWord} ids (page {Math.Min(page, pageCount)}/{pageCount}):");

        foreach (var (id, name) in definitions.Skip((page - 1) * PageSize).Take(PageSize))
            ChatHelper.SendSystemMessage(invoker, $"{id,-6} {name}{(seen.Contains(id) ? " (seen)" : string.Empty)}");

        return true;
    }

    private bool HandleSeen(Player invoker, string[] args)
    {
        if (!TryGetTarget(invoker, args, out var target))
            return true;

        var seen = TutorialHelper.GetSet(target, Set).Order().ToList();

        ChatHelper.SendSystemMessage(invoker, seen.Count == 0
            ? $"{target.Name.FullName} has not seen any {KeyWord} ids."
            : $"{target.Name.FullName} has seen {seen.Count}: {string.Join(", ", seen)}");

        return true;
    }

    private bool HandleMark(Player invoker, string[] args, bool seen)
    {
        if (args.Length == 0)
            return false;

        var definitions = TutorialHelper.GetDefinitions(_resourceManager, Set);

        List<int> ids;

        if (seen && args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            ids = definitions.Keys.ToList();
        else if (TryResolveId(args[0], out var id))
            ids = [id];
        else
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown {KeyWord} \"{args[0]}\". Use !{KeyWord} list.");
            return true;
        }

        if (!TryGetTarget(invoker, args[1..], out var target))
            return true;

        var changed = TutorialHelper.SetSeen(_dbContextFactory, target, Set, ids, seen);

        _chatCommandManager.LogAction(this, invoker, seen ? "Mark" : "Unmark", target.Name.FullName, $"Ids: {args[0]}");
        ChatHelper.SendSystemMessage(invoker, changed
            ? $"{(seen ? "Marked" : "Unmarked")} {args[0]} for {target.Name.FullName}. It takes effect after relog."
            : "Nothing changed.");

        return true;
    }

    private bool HandleReset(Player invoker, string[] args)
    {
        if (!TryGetTarget(invoker, args, out var target))
            return true;

        TutorialHelper.Clear(_dbContextFactory, target, Set);

        _chatCommandManager.LogAction(this, invoker, "Reset", target.Name.FullName, null);
        ChatHelper.SendSystemMessage(invoker, $"Reset all {KeyWord} ids for {target.Name.FullName}. They show again after relog.");

        return true;
    }

    private bool TryResolveId(string value, out int id)
    {
        var definitions = TutorialHelper.GetDefinitions(_resourceManager, Set);

        if (int.TryParse(value, out id))
            return definitions.ContainsKey(id);

        var match = definitions.FirstOrDefault(x => x.Value.Equals(value, StringComparison.OrdinalIgnoreCase));

        id = match.Key;
        return match.Value is not null;
    }

    private bool TryGetTarget(Player invoker, string[] nameArgs, out Player target)
    {
        target = invoker;

        if (nameArgs.Length == 0)
            return true;

        var targetName = string.Join(' ', nameArgs);

        if (_zoneManager.TryGetPlayer(targetName, out var found))
        {
            target = found;
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
        return false;
    }
}
