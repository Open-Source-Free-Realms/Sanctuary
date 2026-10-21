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

        writer.Write(Unknown);
        writer.Write(Unknown2);


        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantRequestExitPacket encounterParticipantRequestExitPacket)
    {
        encounterParticipantRequestExitPacket = new EncounterParticipantRequestExitPacket();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != BaseEncounterPacket.OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out encounterParticipantRequestExitPacket.Unknown))
            return false;

        if (!reader.TryRead(out encounterParticipantRequestExitPacket.Unknown2))
            return false;



        return reader.RemainingLength == 0;
    }
}
