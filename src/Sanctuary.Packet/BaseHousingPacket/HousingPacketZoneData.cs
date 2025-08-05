using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketZoneData : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 45;

    public bool IsPreview;

    private bool Unused = default;

    public int HeadSize;

    public PlayerHousingInstanceInfo InstanceInfo = new();

    public HousingPacketZoneData() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(IsPreview);
        writer.Write(Unused);
        writer.Write(HeadSize);

        InstanceInfo.Serialize(writer);

        return writer.Buffer;
    }
}