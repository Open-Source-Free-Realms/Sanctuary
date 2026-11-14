using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class EncounterTeamInfo : ISerializableType, IDeserializableType
{
    public int Unknown;

    public int Unknown2;

    public int Unknown3;

    public int Unknown4;

    public int Unknown5;

    public int Unknown6;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Unknown);

        writer.Write(Unknown2);

        writer.Write(Unknown3);

        writer.Write(Unknown4);

        writer.Write(Unknown5);

        writer.Write(Unknown6);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out Unknown))
            return false;

        if (!reader.TryRead(out Unknown2))
            return false;

        if (!reader.TryRead(out Unknown3))
            return false;

        if (!reader.TryRead(out Unknown4))
            return false;

        if (!reader.TryRead(out Unknown5))
            return false;

        if (!reader.TryRead(out Unknown6))
            return false;

        return true;
    }
}
