using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetTactileNotify : PetBasePacket, IDeserializable<PetTactileNotify>
{
    public new const byte OpCode = 22;

    public int Mode;

    public int Unknown2;

    public int InputDurationMs;

    public int InputMovementSquaredSum;

    public PetTactileNotify() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetTactileNotify petTactileNotify)
    {
        petTactileNotify = new PetTactileNotify();

        var reader = new PacketReader(data);

        if (!petTactileNotify.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petTactileNotify.Mode))
            return false;

        if (!reader.TryRead(out petTactileNotify.Unknown2))
            return false;

        if (!reader.TryRead(out petTactileNotify.InputDurationMs))
            return false;

        if (!reader.TryRead(out petTactileNotify.InputMovementSquaredSum))
            return false;

        return reader.RemainingLength == 0;
    }
}
