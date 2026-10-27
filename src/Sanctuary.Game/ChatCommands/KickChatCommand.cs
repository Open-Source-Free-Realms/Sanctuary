using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class KickChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "kick";
    public string Usage => "<player>";
    public string Description => "Disconnects an online player.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public KickChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        var targetName = string.Join(' ', args);

        if (!_zoneManager.TryGetPlayer(targetName, out var target))
        {
            ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
            return true;
        }

        if (target == invoker)
        {
            ChatHelper.SendSystemMessage(invoker, "You cannot kick yourself.");
            return true;
        }

        if ((target.IsMod || target.IsAdmin) && invoker.ChatCommandRole <= target.ChatCommandRole)
        {
            ChatHelper.SendSystemMessage(invoker, $"You are not authorized to kick {targetName}.");
            return true;
        }

        ChatHelper.SendSystemMessage(target, "You have been kicked from the server.");

        target.Disconnect();

        _chatCommandManager.LogAction(this, invoker, "Kick", targetName, null);

        ChatHelper.SendSystemMessage(invoker, $"{targetName} has been kicked.");

        return true;
    }
}
