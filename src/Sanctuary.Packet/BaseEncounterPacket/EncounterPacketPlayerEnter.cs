using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterPacketPlayerEnter : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterPacketPlayerEnter>
{
    public new const short OpCode = 2;

    public ulong PlayerGuid;
    public bool IsInNPXTutorial;

    public EncounterPacketPlayerEnter() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PlayerGuid);
        writer.Write(IsInNPXTutorial);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterPacketPlayerEnter encounterPacketPlayerEnter)
    {
        encounterPacketPlayerEnter = new EncounterPacketPlayerEnter();

        var reader = new PacketReader(data);

        if (!encounterPacketPlayerEnter.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out encounterPacketPlayerEnter.PlayerGuid))
            return false;

        if (!reader.TryRead(out encounterPacketPlayerEnter.IsInNPXTutorial))
            return false;

        return reader.RemainingLength == 0;
    }
}
