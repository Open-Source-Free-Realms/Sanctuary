using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MiniGameStartGamePacket : BaseMiniGamePacket, IDeserializable<MiniGameStartGamePacket>
{
    public new const byte OpCode = 5;

    public MiniGameStartGamePacket() : base(OpCode, 0, -1, -1)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MiniGameStartGamePacket value)
    {
        value = new MiniGameStartGamePacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}