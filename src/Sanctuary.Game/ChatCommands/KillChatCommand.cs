using System;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Players have no server-side health yet, so this only drives the client's hitpoint display.
/// </summary>
public class KillChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "kill";
    public string Usage => "[player] | revive [player] | hp <current> [player]";
    public string Description => "Sets a player's hitpoints to 0, back to full, or to a given value (client-side only; no server health yet).";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public KillChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        var action = "Kill";
        int? hitpoints = 0;

        if (args.Length > 0 && args[0].Equals("revive", StringComparison.OrdinalIgnoreCase))
        {
            action = "Revive";
            hitpoints = null;
            args = args[1..];
        }
        else if (args.Length > 0 && args[0].Equals("hp", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2 || !int.TryParse(args[1], out var value) || value < 0)
                return false;

            action = "SetHitpoints";
            hitpoints = value;
            args = args[2..];
        }

        var target = invoker;

        if (args.Length > 0)
        {
            var targetName = string.Join(' ', args);

            if (!_zoneManager.TryGetPlayer(targetName, out target))
            {
                ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
                return true;
            }
        }

        var maxHitpoints = target.Stats.TryGetValue(CharacterStatId.MaxHealth, out var maxHealth) ? maxHealth.Int : 2500;
        var current = Math.Min(hitpoints ?? maxHitpoints, maxHitpoints);

        target.SendTunneled(new ClientUpdatePacketHitpoints
        {
            CurrentHitpoints = current,
            MaxHitpoints = maxHitpoints
        });

        _chatCommandManager.LogAction(this, invoker, action, target.Name.FullName, $"Hitpoints: {current}/{maxHitpoints}");
        ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName} hitpoints set to {current}/{maxHitpoints}.");

        return true;
    }
}
