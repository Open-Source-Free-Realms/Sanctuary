using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterRunningListResponsePacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 130;

    public List<EncounterDetailsResponsePacket> Encounters { get; } = [];

    public EncounterRunningListResponsePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Encounters.Count);

        foreach (var encounter in Encounters)
            encounter.WriteEncounterDetailsCommon(writer);

        return writer.Buffer;
    }
}
