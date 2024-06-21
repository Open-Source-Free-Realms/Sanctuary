using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketInstanceList : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 39;

    public List<PlayerHousingInstanceInfo> Instances = new();

    public ulong PlayerGuid;

    public HousingPacketInstanceList() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Instances);

        writer.Write(PlayerGuid);

        return writer.Buffer;
    }
}