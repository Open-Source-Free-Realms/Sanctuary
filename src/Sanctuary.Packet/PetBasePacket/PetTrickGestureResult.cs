using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetTrickGestureResult : PetBasePacket, IDeserializable<PetTrickGestureResult>
{
    public new const byte OpCode = 8;

    public int Unknown;

    public PetTrickGestureResult() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetTrickGestureResult petTrickGestureResult)
    {
        petTrickGestureResult = new PetTrickGestureResult();

        var reader = new PacketReader(data);

        if (!petTrickGestureResult.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petTrickGestureResult.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}
