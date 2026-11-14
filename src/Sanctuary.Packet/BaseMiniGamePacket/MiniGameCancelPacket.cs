using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MiniGameCancelPacket : BaseMiniGamePacket, ISerializablePacket, IDeserializable<MiniGameCancelPacket>
{
    public new const byte OpCode = 7;

    public MiniGameCancelPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MiniGameCancelPacket miniGameCancelPacket)
    {
        miniGameCancelPacket = new MiniGameCancelPacket();

        var reader = new PacketReader(data);

        if (!miniGameCancelPacket.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
