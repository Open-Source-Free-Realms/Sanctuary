using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MiniGamePayloadPacket : BaseMiniGamePacket, ISerializablePacket, IDeserializable<MiniGamePayloadPacket>
{
    public new const byte OpCode = 14;

    public byte[] Payload = [];

    public MiniGamePayloadPacket() : base(OpCode, 0, -1, -1)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.WritePayload(Payload);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MiniGamePayloadPacket value)
    {
        value = new MiniGamePayloadPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out int payloadSize))
            return false;

        if (!reader.TryReadExact(payloadSize, out var payload))
            return false;

        value.Payload = new byte[payloadSize];

        if (!payload.TryCopyTo(value.Payload))
            return false;

        return reader.RemainingLength == 0;
    }
}