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

        writer.Write(Unknown);
        writer.Write(Unknown2);


        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantCancelPendingEncounterPacket encounterParticipantCancelPendingEncounterPacket)
    {
        encounterParticipantCancelPendingEncounterPacket = new EncounterParticipantCancelPendingEncounterPacket();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != BaseEncounterPacket.OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out encounterParticipantCancelPendingEncounterPacket.Unknown))
            return false;

        if (!reader.TryRead(out encounterParticipantCancelPendingEncounterPacket.Unknown2))
            return false;



        return reader.RemainingLength == 0;
    }
}
