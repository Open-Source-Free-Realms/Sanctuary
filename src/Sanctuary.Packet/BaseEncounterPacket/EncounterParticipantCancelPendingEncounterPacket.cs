using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterParticipantCancelPendingEncounterPacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterParticipantCancelPendingEncounterPacket>
{
    public new const short OpCode = 124;

    public EncounterParticipantCancelPendingEncounterPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantCancelPendingEncounterPacket encounterParticipantCancelPendingEncounterPacket)
    {
        encounterParticipantCancelPendingEncounterPacket = new EncounterParticipantCancelPendingEncounterPacket();

        var reader = new PacketReader(data);

        if (!encounterParticipantCancelPendingEncounterPacket.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
