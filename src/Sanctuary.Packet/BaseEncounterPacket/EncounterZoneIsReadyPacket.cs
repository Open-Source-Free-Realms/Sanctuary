using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op41 sub107 (S2C) — flips the encounter offer popup's loading spinner into the green GO! button
// ("MiniGame:SetMiniGameReady"). The body is JUST the 12-byte encounter header: the client validator
// requires zero leftover bytes, so any extra payload gets the packet silently rejected.
public class EncounterZoneIsReadyPacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 107;

    public EncounterZoneIsReadyPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // the header is the entire packet

        return writer.Buffer;
    }
}
