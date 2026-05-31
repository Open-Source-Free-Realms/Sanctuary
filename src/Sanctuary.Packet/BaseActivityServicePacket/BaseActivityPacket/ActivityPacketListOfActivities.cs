using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ActivityPacketListOfActivities : BaseActivityPacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    // 1 - PacketTunneledClientWorldPacket
    // 2 - PacketTunneledClientPacket
    public int ServerType;

    public List<ClientActivityDefinition> Activities = [];

    public ActivityPacketListOfActivities() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(ServerType);

        writer.Write(Activities);

        return writer.Buffer;
    }
}