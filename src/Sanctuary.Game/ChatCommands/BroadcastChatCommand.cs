using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class BroadcastChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "broadcast";
    public string Usage => "<message> | zone <message>";
    public string Description => "Sends a system message to every player online, or only to players in your zone.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public BroadcastChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        var zoneOnly = args.Length > 1 && args[0].Equals("zone", System.StringComparison.OrdinalIgnoreCase);

        var message = string.Join(' ', zoneOnly ? args[1..] : args);

        var recipients = zoneOnly
            ? invoker.Zone.Players.ToList()
            : _zoneManager.Zones.SelectMany(zone => zone.Players).ToList();

        foreach (var player in recipients)
            ChatHelper.SendSystemMessage(player, $"[Broadcast] {message}");

        _chatCommandManager.LogAction(this, invoker, zoneOnly ? "ZoneBroadcast" : "Broadcast", null,
            $"Recipients: {recipients.Count}, Message: \"{message}\"");

        return true;
    }
}
