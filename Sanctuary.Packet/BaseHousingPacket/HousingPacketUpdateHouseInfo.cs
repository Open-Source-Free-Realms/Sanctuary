using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class HousingPacketUpdateHouseInfo : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 44;

    public bool InEditMode;
    public bool IsLocked;
    public bool Unknown4;
    public bool PetAutospawn;
    public int Unknown2;
    public int CurFixtureCount;
    public int Unknown7;
    public int FurnitureScore;

    public HousingPacketUpdateHouseInfo() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(InEditMode);

        writer.Write(Unknown2);

        writer.Write(IsLocked);
        writer.Write(Unknown4);
        writer.Write(PetAutospawn);

        writer.Write(CurFixtureCount);
        writer.Write(Unknown7);
        writer.Write(FurnitureScore);

        return writer.Buffer;
    }
}