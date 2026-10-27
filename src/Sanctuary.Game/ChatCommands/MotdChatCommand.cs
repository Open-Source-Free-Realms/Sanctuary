using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class MotdChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IServerSettings _serverSettings;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "motd";
    public string Usage => "[message] | title <title> | clear";
    public string Description => "Shows or sets the message of the day sent to players when they enter the world. Setting it also pushes it to everyone online.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public MotdChatCommand(IZoneManager zoneManager, IServerSettings serverSettings, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _serverSettings = serverSettings;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
        {
            ChatHelper.SendSystemMessage(invoker, string.IsNullOrEmpty(_serverSettings.MotdMessage)
                ? "No message of the day is set."
                : $"{_serverSettings.MotdTitle ?? "Message of the Day"}: {_serverSettings.MotdMessage}");
            return true;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "clear":
                _serverSettings.MotdTitle = null;
                _serverSettings.MotdMessage = null;

                _chatCommandManager.LogAction(this, invoker, "Clear", null, null);
                ChatHelper.SendSystemMessage(invoker, "Message of the day cleared.");
                return true;

            case "title" when args.Length > 1:
                _serverSettings.MotdTitle = string.Join(' ', args[1..]);

                _chatCommandManager.LogAction(this, invoker, "SetTitle", null, $"Title: \"{_serverSettings.MotdTitle}\"");
                ChatHelper.SendSystemMessage(invoker, $"Message of the day title set to \"{_serverSettings.MotdTitle}\".");
                return true;

            case "title":
                return false;
        }

        _serverSettings.MotdMessage = string.Join(' ', args);

        var packetMotd = _serverSettings.CreateMotd();

        if (packetMotd is not null)
        {
            foreach (var player in _zoneManager.Zones.SelectMany(zone => zone.Players))
                player.SendTunneled(packetMotd);
        }

        _chatCommandManager.LogAction(this, invoker, "Set", null, $"Message: \"{_serverSettings.MotdMessage}\"");
        ChatHelper.SendSystemMessage(invoker, "Message of the day set.");

        return true;
    }
}
