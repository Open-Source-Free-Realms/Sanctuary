using Sanctuary.Packet;
using Sanctuary.Packet.Common.Chat;
using Sanctuary.Game.Entities;

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
}
