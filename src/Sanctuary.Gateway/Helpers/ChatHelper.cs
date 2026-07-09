using Sanctuary.Packet;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Helpers;

public static class ChatHelper
{
    public static void SendSystemMessage(GatewayConnection connection, string message)
    {
        connection.Player.SendTunneled(new PacketChat
        {
            Channel = ChatChannel.System,
            Message = message
        });
    }
}
