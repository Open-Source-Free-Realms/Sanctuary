using System.Linq;

using Sanctuary.Database;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Game.Helpers;

public static class ChatHelper
{
    public static void SendSystemMessage(Player player, string message)
    {
        player.SendTunneled(new PacketChat
        {
            Channel = ChatChannel.System,
            Message = message
        });
    }

    public static bool TryResolveTarget(Player invoker, DatabaseContext dbContext, string targetName, out ulong targetUserId)
    {
        var target = dbContext.Characters
            .Where(character => character.FullName == targetName)
            .Select(character => new { character.UserId, character.User.IsAdmin, character.User.IsMod })
            .SingleOrDefault();

        if (target is null)
        {
            SendSystemMessage(invoker, $"No player named \"{targetName}\" was found.");
            targetUserId = 0;
            return false;
        }

        var targetRole = GetRoleFromFlags(target.IsAdmin, target.IsMod);
        if (invoker.ChatCommandRole <= targetRole)
        {
            SendSystemMessage(invoker, "You don't have permission to target this player.");
            targetUserId = 0;
            return false;
        }

        targetUserId = target.UserId;
        return true;
    }

    public static ChatCommandRole GetRoleFromFlags(bool isAdmin, bool isMod)
    {
        if (isAdmin)
            return ChatCommandRole.Admin;

        if (isMod)
            return ChatCommandRole.Mod;

        return ChatCommandRole.Player;
    }
}
