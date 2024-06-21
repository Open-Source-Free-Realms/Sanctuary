using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CharacterCreateRequest : IDeserializable<CharacterCreateRequest>
{
    public const byte OpCode = 5;

    public int ServerId;

    public byte[] Payload = Array.Empty<byte>();

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CharacterCreateRequest value)
    {
        value = new CharacterCreateRequest();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.ServerId))
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

    public override string ToString()
    {
        return $"{nameof(ServerId)}: {ServerId}, {nameof(Payload)}: \"{Convert.ToHexString(Payload)}\"";
    }
}