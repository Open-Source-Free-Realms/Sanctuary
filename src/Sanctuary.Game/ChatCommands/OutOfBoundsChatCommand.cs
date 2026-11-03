using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class OutOfBoundsChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "oob";
    public string Usage => "";
    public string Description => "Lists players currently on out of bounds tiles.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public OutOfBoundsChatCommand(IZoneManager zoneManager)
    {
        _zoneManager = zoneManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        var outOfBoundsPlayers = _zoneManager.Zones
            .SelectMany(zone => zone.Players)
            .Where(player => player.Visible && player.Zone.IsOutOfBounds(player.ZoneTile))
            .ToList();

        if (outOfBoundsPlayers.Count == 0)
        {
            ChatHelper.SendSystemMessage(invoker, "No players are out of bounds.");
            return true;
        }

        foreach (var player in outOfBoundsPlayers)
        {
            var position = player.Position;
            ChatHelper.SendSystemMessage(invoker,
                $"{player.Name.FullName} - {player.Zone.Name} ({position.X:F2}, {position.Y:F2}, {position.Z:F2}) tile {player.ZoneTile}");
        }

        return true;
    }
}
