using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketHousingError : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 46;

    public HousingError Error;

    public HousingPacketHousingError() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write((int)Error);

        return writer.Buffer;
    }
}