using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterOverworldCombatPacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterOverworldCombatPacket>
{
    public new const short OpCode = 132;

    public bool InWorldCombat;

    public EncounterOverworldCombatPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(InWorldCombat);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterOverworldCombatPacket encounterOverworldCombatPacket)
    {
        encounterOverworldCombatPacket = new EncounterOverworldCombatPacket();

        var reader = new PacketReader(data);

        if (!encounterOverworldCombatPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out encounterOverworldCombatPacket.InWorldCombat))
            return false;

        return reader.RemainingLength == 0;
    }
}