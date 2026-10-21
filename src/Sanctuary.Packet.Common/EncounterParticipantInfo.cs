using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class EncounterParticipantInfo : ISerializableType, IDeserializableType
{
    public ulong Unknown;

    public bool Unknown2;

    public int Unknown3;

    public bool Unknown6;

    public bool Unknown7;

    public bool Unknown8;

    public bool Unknown9;

    public bool Unknown17;

    public bool Unknown18;

    public Vector4 Unknown10;

    public string? Unknown11;

    public bool Unknown12;

    public string? Unknown13;

    public int Unknown14;

    public int Unknown15;

    public int Unknown16;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Unknown);

        writer.Write(Unknown2);

        writer.Write(Unknown3);

        writer.Write(Unknown6);

        writer.Write(Unknown7);

        writer.Write(Unknown8);

        writer.Write(Unknown9);

        writer.Write(Unknown17);

        writer.Write(Unknown18);

        writer.Write(Unknown10);

        writer.Write(Unknown11);

        writer.Write(Unknown12);

        writer.Write(Unknown13);

        writer.Write(Unknown14);

        writer.Write(Unknown15);

        writer.Write(Unknown16);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out Unknown))
            return false;

        if (!reader.TryRead(out Unknown2))
            return false;

        if (!reader.TryRead(out Unknown3))
            return false;

        if (!reader.TryRead(out Unknown6))
            return false;

        if (!reader.TryRead(out Unknown7))
            return false;

        if (!reader.TryRead(out Unknown8))
            return false;

        if (!reader.TryRead(out Unknown9))
            return false;

        if (!reader.TryRead(out Unknown17))
            return false;

        if (!reader.TryRead(out Unknown18))
            return false;

        if (!reader.TryRead(out Unknown10))
            return false;

        var unknown11Reader = reader;
        if (!unknown11Reader.TryRead(out int unknown11Length) || unknown11Length < 0 || unknown11Length > unknown11Reader.RemainingLength)
            return false;

        if (!reader.TryRead(out Unknown11))
            return false;

        if (!reader.TryRead(out Unknown12))
            return false;

        var unknown13Reader = reader;
        if (!unknown13Reader.TryRead(out int unknown13Length) || unknown13Length < 0 || unknown13Length > unknown13Reader.RemainingLength)
            return false;

        if (!reader.TryRead(out Unknown13))
            return false;

        if (!reader.TryRead(out Unknown14))
            return false;

        if (!reader.TryRead(out Unknown15))
            return false;

        if (!reader.TryRead(out Unknown16))
            return false;

        return true;
    }
}
