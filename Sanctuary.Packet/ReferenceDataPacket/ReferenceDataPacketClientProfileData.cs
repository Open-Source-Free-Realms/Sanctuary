using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ReferenceDataPacketClientProfileData : ReferenceDataPacket, ISerializablePacket
{
    public new const short OpCode = 3;

    public Dictionary<int, ClientProfileData> Profiles = new();

    public ReferenceDataPacketClientProfileData() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Profiles);

        return writer.Buffer;
    }
}