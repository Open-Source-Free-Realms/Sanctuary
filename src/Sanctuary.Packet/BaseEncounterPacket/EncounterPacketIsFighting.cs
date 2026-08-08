using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op41 sub133 — client BaseClient::SetIsFighting. Together with sub132 (EncounterOverworldCombatPacket)
// this opens the client-side gate that otherwise suppresses floating combat text (damage numbers, MISS!).
public class EncounterPacketIsFighting : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 133;

    public bool InWorldCombat;

    public EncounterPacketIsFighting() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(InWorldCombat);

        return writer.Buffer;
    }
}
