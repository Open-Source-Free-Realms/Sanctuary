using System.Linq;

using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

public static class ChatHelper
{
    /// <summary>
    /// Chat line the patched chat window swallows to unhide the GM panel.
    /// </summary>
    public const string GmPanelToken = "[[SANCTUARY_GM]]";

    /// <summary>
    /// Sends data to the patched chat window's GM panel as a hidden chat line:
    /// <c>[[SANCTUARY_GM]]|kind|field|field...</c>
    /// </summary>
    public static void SendGmPanelData(Player player, string kind, params object[] fields)
    {
        var values = fields.Select(field => (field?.ToString() ?? string.Empty).Replace("|", "/"));

        SendSystemMessage(player, string.Join('|', [GmPanelToken, kind, .. values]));
    }

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

#if DEBUG
        return ChatCommandRole.Admin;
#else
        return ChatCommandRole.Player;
#endif
    }
}
