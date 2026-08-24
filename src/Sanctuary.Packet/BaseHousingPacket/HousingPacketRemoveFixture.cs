using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class HousingPacketRemoveFixture : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 41;

    public ulong FixtureGuid;

    public HousingPacketRemoveFixture() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(FixtureGuid);

        return writer.Buffer;
    }
}
