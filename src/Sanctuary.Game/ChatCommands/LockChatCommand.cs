using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class LockChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IServerSettings _serverSettings;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "lock";
    public string Usage => "[on [kick] | off]";
    public string Description => "Locks the world so only referees can enter. 'on kick' also disconnects everyone else. Resets on restart.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public LockChatCommand(IZoneManager zoneManager, IServerSettings serverSettings, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _serverSettings = serverSettings;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
        {
            ChatHelper.SendSystemMessage(invoker, $"The world is {(_serverSettings.IsLocked ? "locked" : "unlocked")}.");
            return true;
        }

        if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase) && args.Length == 1)
        {
            _serverSettings.IsLocked = false;

            _chatCommandManager.LogAction(this, invoker, "Unlock", null, null);
            ChatHelper.SendSystemMessage(invoker, "The world is now unlocked.");
            return true;
        }

        if (!args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
            return false;

        var kick = args.Length == 2 && args[1].Equals("kick", StringComparison.OrdinalIgnoreCase);

        if (args.Length > 1 && !kick)
            return false;

        _serverSettings.IsLocked = true;

        var kicked = 0;

        if (kick)
        {
            var players = _zoneManager.Zones
                .SelectMany(zone => zone.Players)
                .Where(player => !player.IsMod && !player.IsAdmin)
                .ToList();

            foreach (var player in players)
            {
                ChatHelper.SendSystemMessage(player, "The server has been locked for maintenance.");
                player.Disconnect();
            }

            kicked = players.Count;
        }

        _chatCommandManager.LogAction(this, invoker, "Lock", null, kick ? $"Kicked: {kicked}" : null);
        ChatHelper.SendSystemMessage(invoker, kick
            ? $"The world is now locked. Kicked {kicked} player(s)."
            : "The world is now locked.");

        return true;
    }
}
