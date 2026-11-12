using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GatewayCharacterIdleRequest : ISerializablePacket, IDeserializable<GatewayCharacterIdleRequest>
{
    public const byte OpCode = 105;

    public uint RequestId;

    public ulong Id;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(RequestId);

        writer.Write(Id);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GatewayCharacterIdleRequest value)
    {
        value = new GatewayCharacterIdleRequest();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.RequestId) || !reader.TryRead(out value.Id))
            return false;

        return reader.RemainingLength == 0;
    }
}
