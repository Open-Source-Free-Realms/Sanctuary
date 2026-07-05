using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class HousingPacketFixtureRemove : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 41;

    public ulong FixtureGuid;

    public HousingPacketFixtureRemove() : base(OpCode)
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
