using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterOverworldCombatPacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 132;

    public bool Unknown3;

    public EncounterOverworldCombatPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown3);

        return writer.Buffer;
    }
}