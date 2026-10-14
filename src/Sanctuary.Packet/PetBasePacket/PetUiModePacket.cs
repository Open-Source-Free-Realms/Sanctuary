using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetUiModePacket : PetBasePacket, IDeserializable<PetUiModePacket>
{
    public new const byte OpCode = 24;

    public int Active;

    public PetUiModePacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PetUiModePacket petUiModePacket)
    {
        petUiModePacket = new PetUiModePacket();

        var reader = new PacketReader(data);

        if (!petUiModePacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out petUiModePacket.Active))
            return false;

        return reader.RemainingLength == 0;
    }
}
