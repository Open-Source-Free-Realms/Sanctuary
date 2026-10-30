using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterParticipantRequestExitPacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterParticipantRequestExitPacket>
{
    public new const short OpCode = 109;

    public EncounterParticipantRequestExitPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantRequestExitPacket encounterParticipantRequestExitPacket)
    {
        encounterParticipantRequestExitPacket = new EncounterParticipantRequestExitPacket();

        var reader = new PacketReader(data);

        if (!encounterParticipantRequestExitPacket.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
