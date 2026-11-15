using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MiniGameKnockOutPacket : BaseMiniGamePacket, ISerializablePacket, IDeserializable<MiniGameKnockOutPacket>
{
    public new const byte OpCode = 23;

    public int KnockOuts;

    public int MaxKnockOuts;

    public MiniGameKnockOutPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(KnockOuts);
        writer.Write(MaxKnockOuts);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MiniGameKnockOutPacket miniGameKnockOutPacket)
    {
        miniGameKnockOutPacket = new MiniGameKnockOutPacket();

        var reader = new PacketReader(data);

        if (!miniGameKnockOutPacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out miniGameKnockOutPacket.KnockOuts))
            return false;

        if (!reader.TryRead(out miniGameKnockOutPacket.MaxKnockOuts))
            return false;

        return reader.RemainingLength == 0;
    }
}
