using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class HousingPacketHousingError : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 46;

    public int Error;

    public HousingPacketHousingError() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Error);

        return writer.Buffer;
    }
}