using System;
using System.Linq;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Traces a player's gateway traffic to Logs/PacketLog-*.log (see NLog.config).
/// </summary>
public class PacketLogChatCommand : IChatCommand
{
    private readonly ILogger _packetLogger;
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "packetlog";
    public string Usage => "on|off [player] | list";
    public string Description => "Logs every packet to and from a player (default: you) to Logs/PacketLog-<date>.log.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public PacketLogChatCommand(ILoggerFactory loggerFactory, IZoneManager zoneManager, IChatCommandManager chatCommandManager)
    {
        _packetLogger = loggerFactory.CreateLogger("PacketLog");
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            var logged = _zoneManager.Zones
                .SelectMany(zone => zone.Players)
                .Where(player => player.PacketLogger is not null)
                .Select(player => player.Name.FullName)
                .ToList();

            ChatHelper.SendSystemMessage(invoker, logged.Count == 0
                ? "No players are being packet logged."
                : $"Packet logging: {string.Join(", ", logged)}");
            return true;
        }

        bool enable;

        if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
            enable = true;
        else if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
            enable = false;
        else
            return false;

        var target = invoker;

        if (args.Length > 1)
        {
            var targetName = string.Join(' ', args[1..]);

            if (!_zoneManager.TryGetPlayer(targetName, out target))
            {
                ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
                return true;
            }
        }

        target.PacketLogger = enable ? _packetLogger : null;

        _chatCommandManager.LogAction(this, invoker, enable ? "On" : "Off", target.Name.FullName, null);
        ChatHelper.SendSystemMessage(invoker, $"Packet logging {(enable ? "enabled" : "disabled")} for {target.Name.FullName}.");

        return true;
    }
}
