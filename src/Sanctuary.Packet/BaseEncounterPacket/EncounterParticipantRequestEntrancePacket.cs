using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterParticipantRequestEntrancePacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterParticipantRequestEntrancePacket>
{
    public new const short OpCode = 108;

    public ulong Guid;

    public EncounterParticipantRequestEntrancePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantRequestEntrancePacket encounterParticipantRequestEntrancePacket)
    {
        encounterParticipantRequestEntrancePacket = new EncounterParticipantRequestEntrancePacket();

        var reader = new PacketReader(data);

        if (!encounterParticipantRequestEntrancePacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out encounterParticipantRequestEntrancePacket.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
