using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class HousingPacketUpdateHouseInfo : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 44;

    public bool InEditMode;
    public bool IsLocked;
    public bool IsFloraAllowed;

    public bool PetAutospawn;

    private int Unused = default;

    public int CurFixtureCount;
    public int CurLandmarkCount;

    public int FurnitureScore;

    public HousingPacketUpdateHouseInfo() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(InEditMode);

        writer.Write(Unused);

        writer.Write(IsLocked);
        writer.Write(IsFloraAllowed);
        writer.Write(PetAutospawn);

        writer.Write(CurFixtureCount);
        writer.Write(CurLandmarkCount);

        writer.Write(FurnitureScore);

        return writer.Buffer;
    }
}