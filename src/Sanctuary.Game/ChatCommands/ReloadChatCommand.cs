using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class ReloadChatCommand : IChatCommand
{
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "reload";
    public string Usage => "<resource> | all | list";
    public string Description => "Reloads resource data (items, npcs, stores, ...) from disk, replacing existing entries. Zones and maps need a restart.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public ReloadChatCommand(IResourceManager resourceManager, IChatCommandManager chatCommandManager)
    {
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 1)
            return false;

        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            ChatHelper.SendSystemMessage(invoker, $"Reloadable: {string.Join(", ", _resourceManager.ReloadableResources.Order())}");
            return true;
        }

        IEnumerable<string> names = args[0].Equals("all", StringComparison.OrdinalIgnoreCase)
            ? _resourceManager.ReloadableResources.ToList()
            : [args[0]];

        if (!names.All(name => _resourceManager.ReloadableResources.Contains(name, StringComparer.OrdinalIgnoreCase)))
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown resource \"{args[0]}\". Use !{KeyWord} list.");
            return true;
        }

        var failed = names.Where(name => !_resourceManager.TryReload(name)).ToList();

        _chatCommandManager.LogAction(this, invoker, "Reload", null,
            $"Resources: {args[0]}{(failed.Count > 0 ? $", Failed: {string.Join(", ", failed)}" : string.Empty)}");

        ChatHelper.SendSystemMessage(invoker, failed.Count == 0
            ? $"Reloaded {args[0]}."
            : $"Failed to reload {string.Join(", ", failed)}; previous data was kept. Check the server log.");

        return true;
    }
}
