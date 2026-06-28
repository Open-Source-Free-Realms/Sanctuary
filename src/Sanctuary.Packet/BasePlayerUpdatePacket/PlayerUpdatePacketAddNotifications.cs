using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketAddNotifications : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 10;

    public List<NotificationInfo> Notifications = new();

    public PlayerUpdatePacketAddNotifications() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Notifications);

        return writer.Buffer;
    }
}