using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterZoneIsReadyPacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterZoneIsReadyPacket>
{
    public new const short OpCode = 107;

    public EncounterZoneIsReadyPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterZoneIsReadyPacket encounterZoneIsReadyPacket)
    {
        encounterZoneIsReadyPacket = new EncounterZoneIsReadyPacket();

        var reader = new PacketReader(data);

        if (!encounterZoneIsReadyPacket.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
