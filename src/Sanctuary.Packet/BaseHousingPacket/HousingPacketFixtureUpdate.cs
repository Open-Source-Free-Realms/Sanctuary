using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketFixtureUpdate : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 40;

    public FixtureInstance Instance = new();
    public FixtureInstanceInfo Info = new();
    public FixtureDefinition Definition = new();

    public List<ulong> Unknown = new();

    public int Unknown2;
    public int Unknown3;
    public int Unknown4;

    public HousingPacketFixtureUpdate() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Instance.Serialize(writer);
        Info.Serialize(writer);
        Definition.Serialize(writer);

        writer.Write(Unknown);

        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);

        return writer.Buffer;
    }
}