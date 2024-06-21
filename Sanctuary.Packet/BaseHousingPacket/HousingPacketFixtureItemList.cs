using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketFixtureItemList : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 43;

    public List<FixtureInstanceInfo> Infos = new();
    public List<FixtureDefinition> Definitions = new();
    public List<int> Effects = new();

    public HousingPacketFixtureItemList() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Infos);

        writer.Write(Definitions);

        writer.Write(Effects);

        return writer.Buffer;
    }
}