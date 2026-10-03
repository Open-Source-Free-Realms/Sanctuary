using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class FreeInteractionNpc : BaseCommandPacket, ISerializablePacket, IDeserializable<FreeInteractionNpc>
{
    public new const short OpCode = 20;

    public FreeInteractionNpc() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FreeInteractionNpc value)
    {
        value = new FreeInteractionNpc();
        var reader = new PacketReader(data);
        return value.TryRead(ref reader) && reader.RemainingLength == 0;
    }
}
