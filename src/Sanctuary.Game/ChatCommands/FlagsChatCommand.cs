using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class FlagsChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "flags";
    public string Usage => "[player]";
    public string Description => "Shows a player's account/character flags.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public FlagsChatCommand(IZoneManager zoneManager)
    {
        _zoneManager = zoneManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (!TryGetTarget(invoker, args, out var target))
            return true;

        ChatHelper.SendSystemMessage(invoker,
            $"{target.Name.FullName}: Mod={target.IsMod}, Admin={target.IsAdmin}, Muted={target.IsMuted()}, " +
            $"Frozen={target.FrozenMovementSpeed is not null}, PacketLog={target.PacketLogger is not null}");

        return true;
    }

    private bool TryGetTarget(Player invoker, string[] nameArgs, out Player target)
    {
        target = invoker;

        if (nameArgs.Length == 0)
            return true;

        var targetName = string.Join(' ', nameArgs);

        if (_zoneManager.TryGetPlayer(targetName, out var found))
        {
            target = found;
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
        return false;
    }
}
