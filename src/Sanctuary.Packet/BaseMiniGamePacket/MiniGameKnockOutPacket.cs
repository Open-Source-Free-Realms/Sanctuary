using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MiniGameKnockOutPacket : BaseMiniGamePacket, ISerializablePacket, IDeserializable<MiniGameKnockOutPacket>
{
    public new const byte OpCode = 23;

    public int Unknown;

    public int Unknown2;

    public MiniGameKnockOutPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown);
        writer.Write(Unknown2);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MiniGameKnockOutPacket miniGameKnockOutPacket)
    {
        miniGameKnockOutPacket = new MiniGameKnockOutPacket();

        var reader = new PacketReader(data);

        if (!miniGameKnockOutPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out miniGameKnockOutPacket.Unknown))
            return false;

        if (!reader.TryRead(out miniGameKnockOutPacket.Unknown2))
            return false;

        return reader.RemainingLength == 0;
    }
}
