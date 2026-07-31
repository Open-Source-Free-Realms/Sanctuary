using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

public static class ChatHelper
{
    public static void SendSystemMessage(Player player, string message, bool formatted = false)
    {
        player.SendTunneled(new ChatPacketDebugChat
        {
            PrintToChat = true,
            Message = formatted ? message : EscapeMarkup(message)
        });
    }

    private static string EscapeMarkup(string message)
    {
        return message
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
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
