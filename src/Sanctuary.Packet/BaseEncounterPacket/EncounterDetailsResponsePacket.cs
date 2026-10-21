using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class EncounterDetailsResponsePacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterDetailsResponsePacket>
{
    public new const short OpCode = 114;

    public EncounterDetailsInfo Details = new();

    public bool Unknown3;

    public int Unknown4;

    public List<int> Unknown5 { get; } = [];

    public EncounterDetailsResponsePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        Details.Serialize(writer);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5.Count);

        foreach (var id in Unknown5)
            writer.Write(id);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterDetailsResponsePacket encounterDetailsResponsePacket)
    {
        encounterDetailsResponsePacket = new();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != BaseEncounterPacket.OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out encounterDetailsResponsePacket.Unknown))
            return false;

        if (!reader.TryRead(out encounterDetailsResponsePacket.Unknown2))
            return false;

        if (!encounterDetailsResponsePacket.Details.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out encounterDetailsResponsePacket.Unknown3))
            return false;

        if (!reader.TryRead(out encounterDetailsResponsePacket.Unknown4))
            return false;

        if (!reader.TryRead(out int idCount) || idCount < 0 || idCount > reader.RemainingLength / 4)
            return false;

        for (var i = 0; i < idCount; i++)
        {
            if (!reader.TryRead(out int id))
                return false;

            encounterDetailsResponsePacket.Unknown5.Add(id);
        }

        return reader.RemainingLength == 0;
    }
}
