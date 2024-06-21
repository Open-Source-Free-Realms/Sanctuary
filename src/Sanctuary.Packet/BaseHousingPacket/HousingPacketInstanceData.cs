using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketInstanceData : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 28;

    public PlayerHousingInstanceData InstanceData = new();

    public HousingPacketInstanceData() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        InstanceData.Serialize(writer);

        return writer.Buffer;
    }
}