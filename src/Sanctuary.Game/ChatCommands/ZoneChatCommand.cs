using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class ZoneChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "zone";
    public string Usage => "ls";
    public string Description => "Lists all currently running zone instances, their owner, and player count.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public ZoneChatCommand(IZoneManager zoneManager)
    {
        _zoneManager = zoneManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 1 || args[0] != "ls")
            return false;

        var zones = _zoneManager.Zones
            .OrderBy(zone => zone.Id)
            .ToList();

        if (zones.Count == 0)
        {
            ChatHelper.SendSystemMessage(invoker, "No zones currently running.");
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, $"{"ID",-6}{"NAME",-32}{"OWNER",-20}{"PLAYERS",-8}");

        foreach (var zone in zones)
        {
            var owner = zone.OwnerId?.ToString() ?? "none";
            var playerCount = zone.Players.Count();

            ChatHelper.SendSystemMessage(invoker, $"{zone.Id,-6}{zone.Name,-32}{owner,-20}{playerCount,-8}");
        }

        return true;
    }
}
