using Sanctuary.Core.IO;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class AnnouncementBasePacketHandler
{
    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out byte subOpCode) || subOpCode != 1)
            return false;

        connection.SendTunneled(WelcomeScreenHelper.GetAnnouncementsPacket());
        return true;
    }
}
