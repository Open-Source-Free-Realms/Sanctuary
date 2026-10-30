using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterParticipantTerminateEncounterPacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterParticipantTerminateEncounterPacket>
{
    public new const short OpCode = 121;

    public bool Unknown3;

    public EncounterParticipantTerminateEncounterPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown3);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantTerminateEncounterPacket encounterParticipantTerminateEncounterPacket)
    {
        encounterParticipantTerminateEncounterPacket = new EncounterParticipantTerminateEncounterPacket();

        var reader = new PacketReader(data);

        if (!encounterParticipantTerminateEncounterPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out encounterParticipantTerminateEncounterPacket.Unknown3))
            return false;

        return reader.RemainingLength == 0;
    }
}
