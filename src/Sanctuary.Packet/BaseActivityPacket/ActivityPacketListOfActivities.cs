using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ActivityPacketListOfActivities : BaseActivityPacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    public byte Unknown { get; set; }
    public int Unknown2 { get; set; }

    public List<ClientActivityDefinition> Activities { get; } = [];

    public ActivityPacketListOfActivities() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Activities);

        return writer.Buffer;
    }
}
