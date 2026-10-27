using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Freezes by zeroing MaxMovementSpeed. PlayerUpdatePacketFreeze exists in the client,
/// but its layout is not known yet.
/// </summary>
public class FreezeChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "freeze";
    public string Usage => "<player>";
    public string Description => "Toggles a player's ability to move. Clears on relog.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public FreezeChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager)
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

        if (target.FrozenMovementSpeed is { } previousSpeed)
        {
            target.FrozenMovementSpeed = null;
            target.UpdateCharacterStats(new CharacterStat(CharacterStatId.MaxMovementSpeed, previousSpeed));

            _chatCommandManager.LogAction(this, invoker, "Unfreeze", targetName, null);
            ChatHelper.SendSystemMessage(target, "You can move again.");
            ChatHelper.SendSystemMessage(invoker, $"{targetName} unfrozen.");
            return true;
        }

        target.FrozenMovementSpeed = target.Stats.TryGetValue(CharacterStatId.MaxMovementSpeed, out var speed) ? speed.Float : 8f;
        target.UpdateCharacterStats(new CharacterStat(CharacterStatId.MaxMovementSpeed, 0f));

        _chatCommandManager.LogAction(this, invoker, "Freeze", targetName, null);
        ChatHelper.SendSystemMessage(target, "You have been frozen by a referee.");
        ChatHelper.SendSystemMessage(invoker, $"{targetName} frozen. Run !{KeyWord} {targetName} again to unfreeze.");

        return true;
    }
}
