using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterParticipantTerminateEncounterPacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterParticipantTerminateEncounterPacket>
{
    public new const short OpCode = 121;

    public bool TookTooLong;

    public EncounterParticipantTerminateEncounterPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TookTooLong);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantTerminateEncounterPacket encounterParticipantTerminateEncounterPacket)
    {
        encounterParticipantTerminateEncounterPacket = new EncounterParticipantTerminateEncounterPacket();

        var reader = new PacketReader(data);

        if (!encounterParticipantTerminateEncounterPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out encounterParticipantTerminateEncounterPacket.TookTooLong))
            return false;

        return reader.RemainingLength == 0;
    }
}
