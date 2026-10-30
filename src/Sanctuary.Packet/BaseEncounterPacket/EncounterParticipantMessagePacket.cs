using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterParticipantMessagePacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterParticipantMessagePacket>
{
    public new const short OpCode = 120;

    public int MessageId;
    public ulong Guid;
    public string? PlayerName;

    public EncounterParticipantMessagePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(MessageId);
        writer.Write(Guid);
        writer.Write(PlayerName);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantMessagePacket encounterParticipantMessagePacket)
    {
        encounterParticipantMessagePacket = new EncounterParticipantMessagePacket();

        var reader = new PacketReader(data);

        if (!encounterParticipantMessagePacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out encounterParticipantMessagePacket.MessageId))
            return false;

        if (!reader.TryRead(out encounterParticipantMessagePacket.Guid))
            return false;

        var stringReader = reader;
        if (!stringReader.TryRead(out int length) || length < 0 || length > stringReader.RemainingLength)
            return false;

        if (!reader.TryRead(out encounterParticipantMessagePacket.PlayerName))
            return false;

        return reader.RemainingLength == 0;
    }
}
