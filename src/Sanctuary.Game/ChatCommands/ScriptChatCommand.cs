using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Scripting;

namespace Sanctuary.Game.ChatCommands;

public class ScriptChatCommand : IChatCommand
{
    private enum TargetType
    {
        Zone,
        Npc
    }

    private readonly IScriptManager _scriptManager;

    public string KeyWord => "script";

    public string Usage => "reload | add <zone|npc> <scriptName> | remove <zone|npc> <scriptName>";

    public string Description => "Reload, add, or remove scripts for zones and NPCs.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public ScriptChatCommand(IScriptManager scriptManager)
    {
        _scriptManager = scriptManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length < 1)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "reload":
                Reload(invoker);
                return true;
            case "add":
                if (args.Length < 3)
                    return false;

                var addTargetType = ParseTargetType(args[1]);
                if (!addTargetType.HasValue)
                    return false;

                Add(invoker, addTargetType.Value, args[2]);
                return true;
            case "remove":
                if (args.Length < 3)
                    return false;

                var removeTargetType = ParseTargetType(args[1]);
                if (!removeTargetType.HasValue)
                    return false;

                Remove(invoker, removeTargetType.Value, args[2]);
                return true;
            default:
                return false;
        }
    }

    private static TargetType? ParseTargetType(string targetType)
    {
        if (Enum.TryParse<TargetType>(targetType, true, out var parsedTargetType))
            return parsedTargetType;

        return null;
    }

    private void Reload(Player invoker)
    {
        _scriptManager.Reload();

        ChatHelper.SendSystemMessage(invoker, "All scripts have been reloaded.");
    }

    private static void Add(Player invoker, TargetType targetType, string scriptName)
    {
        switch (targetType)
        {
        case TargetType.Zone:
            if (invoker.Zone.TryAddScript(scriptName))
            {
                ChatHelper.SendSystemMessage(invoker, $"Successfully added script {scriptName} to zone {invoker.Zone.Name}.");
            }
            else
            {
                ChatHelper.SendSystemMessage(invoker, $"Script {scriptName} is already added to zone {invoker.Zone.Name}.");
            }
            break;
        case TargetType.Npc:
            var nearestNpc = invoker.Zone.Npcs
                .OrderBy(npc => System.Numerics.Vector4.Distance(npc.Position, invoker.Position))
                .FirstOrDefault();

            if (nearestNpc is null)
            {
                ChatHelper.SendSystemMessage(invoker, "No NPCs found in the zone to add a script to.");
                return;
            }

            if (nearestNpc.TryAddScript(scriptName))
            {
                ChatHelper.SendSystemMessage(invoker, $"Successfully added script {scriptName} to NPC {nearestNpc.Name}.");
            }
            else
            {
                ChatHelper.SendSystemMessage(invoker, $"Script {scriptName} is already added to NPC {nearestNpc.Name}.");
            }
            break;
        default:
            ChatHelper.SendSystemMessage(invoker, $"Unknown target type: {targetType}. Valid types are 'zone' and 'npc'.");
            break;
        }
    }

    private static void Remove(Player invoker, TargetType targetType, string scriptName)
    {
        switch (targetType)
        {
            case TargetType.Zone:
                if (invoker.Zone.TryRemoveScript(scriptName))
                {
                    ChatHelper.SendSystemMessage(invoker, $"Successfully removed script {scriptName} from zone {invoker.Zone.Name}.");
                }
                else
                {
                    ChatHelper.SendSystemMessage(invoker, $"Failed to remove script {scriptName} from zone {invoker.Zone.Name}.");
                }
                break;
            case TargetType.Npc:
                var nearestNpc = invoker.Zone.Npcs
                    .OrderBy(npc => System.Numerics.Vector4.Distance(npc.Position, invoker.Position))
                    .FirstOrDefault();

                if (nearestNpc is null)
                {
                    ChatHelper.SendSystemMessage(invoker, "No NPCs found in the zone to remove a script from.");
                    return;
                }

                if (!nearestNpc.TryRemoveScript(scriptName))
                {
                    ChatHelper.SendSystemMessage(invoker, $"Failed to remove script {scriptName} from NPC {nearestNpc.Name}.");
                    return;
                }

                ChatHelper.SendSystemMessage(invoker, $"Successfully removed script {scriptName} from NPC {nearestNpc.Name}.");
                break;
            default:
                ChatHelper.SendSystemMessage(invoker, $"Unknown target type: {targetType}. Valid types are 'zone' and 'npc'.");
                break;
        }
    }
}