using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketFixtureUpdate : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 40;

    public int Unknown;
    public int Unknown2;
    public int Unknown3;
    public FixtureInstance Fixture = new();
    public List<ulong> UnknownGuids = new();

    public HousingPacketFixtureUpdate() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        Fixture.Serialize(writer);
        writer.Write(UnknownGuids);

        return writer.Buffer;
    }
}
