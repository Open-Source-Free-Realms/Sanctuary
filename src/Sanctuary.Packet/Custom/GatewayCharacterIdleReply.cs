using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GatewayCharacterIdleReply : ISerializablePacket, IDeserializable<GatewayCharacterIdleReply>
{
    public const byte OpCode = 106;

    public uint RequestId;

    public bool Found;

    public long IdleMilliseconds;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(RequestId);

        writer.Write(Found);

        writer.Write(IdleMilliseconds);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GatewayCharacterIdleReply value)
    {
        value = new GatewayCharacterIdleReply();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.RequestId) || !reader.TryRead(out value.Found) || !reader.TryRead(out value.IdleMilliseconds))
            return false;

        return reader.RemainingLength == 0;
    }
}
