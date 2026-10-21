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

        writer.Write(Unknown);
        writer.Write(Unknown2);


        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterZoneIsReadyPacket encounterZoneIsReadyPacket)
    {
        encounterZoneIsReadyPacket = new EncounterZoneIsReadyPacket();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != BaseEncounterPacket.OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out encounterZoneIsReadyPacket.Unknown))
            return false;

        if (!reader.TryRead(out encounterZoneIsReadyPacket.Unknown2))
            return false;



        return reader.RemainingLength == 0;
    }
}
