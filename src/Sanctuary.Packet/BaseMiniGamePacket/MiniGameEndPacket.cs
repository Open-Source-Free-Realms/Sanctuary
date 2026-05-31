using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MiniGameEndPacket : BaseMiniGamePacket, IDeserializable<MiniGameEndPacket>
{
    public new const byte OpCode = 6;

    public MiniGameEndPacket() : base(OpCode, 0, -1, -1)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MiniGameEndPacket value)
    {
        value = new MiniGameEndPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}