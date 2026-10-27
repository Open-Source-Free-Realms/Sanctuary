using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class HousingPacketFixturePlacementDenied : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 53;

    public string? Message;

    public HousingPacketFixturePlacementDenied() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Message);

        return writer.Buffer;
    }
}