using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterStatePacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterStatePacket>
{
    public new const short OpCode = 106;

    public int State;

    public EncounterStatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(State);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterStatePacket encounterStatePacket)
    {
        encounterStatePacket = new EncounterStatePacket();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != BaseEncounterPacket.OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out encounterStatePacket.Unknown))
            return false;

        if (!reader.TryRead(out encounterStatePacket.Unknown2))
            return false;

        if (!reader.TryRead(out encounterStatePacket.State))
            return false;

        return reader.RemainingLength == 0;
    }
}
