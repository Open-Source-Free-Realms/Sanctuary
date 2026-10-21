using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterParticipantMessagePacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterParticipantMessagePacket>
{
    public new const short OpCode = 120;

    public int Unknown3;
    public ulong Guid;
    public string? Unknown4;

    public EncounterParticipantMessagePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Guid);
        writer.Write(Unknown4);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantMessagePacket encounterParticipantMessagePacket)
    {
        encounterParticipantMessagePacket = new EncounterParticipantMessagePacket();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != BaseEncounterPacket.OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out encounterParticipantMessagePacket.Unknown))
            return false;

        if (!reader.TryRead(out encounterParticipantMessagePacket.Unknown2))
            return false;

        if (!reader.TryRead(out encounterParticipantMessagePacket.Unknown3))
            return false;

        if (!reader.TryRead(out encounterParticipantMessagePacket.Guid))
            return false;

        var stringReader = reader;
        if (!stringReader.TryRead(out int length) || length < 0 || length > stringReader.RemainingLength)
            return false;

        if (!reader.TryRead(out encounterParticipantMessagePacket.Unknown4))
            return false;

        return reader.RemainingLength == 0;
    }
}
