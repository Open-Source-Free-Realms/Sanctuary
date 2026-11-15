using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class HousingPacketNotifyHouseAdded : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 49;

    public int NameId;

    public ulong HouseGuid;
    public ulong OwnerGuid;

    public HousingPacketNotifyHouseAdded() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(NameId);

        writer.Write(HouseGuid);
        writer.Write(OwnerGuid);

        return writer.Buffer;
    }
}