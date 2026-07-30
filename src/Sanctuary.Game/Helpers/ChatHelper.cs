using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Game.Helpers;

public static class ChatHelper
{
    public static void SendSystemMessage(Player player, string message)
    {
        player.SendTunneled(new ChatPacketDebugChat
        {
            PrintToChat = true,
            Message = message
        });
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
