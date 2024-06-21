using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketTunneledClientWorldPacket : ISerializablePacket, IDeserializable<PacketTunneledClientWorldPacket>
{
    public const short OpCode = 6;

    public bool Reliable = true;

    public byte[] Payload = Array.Empty<byte>();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Reliable);

        writer.WritePayload(Payload);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketTunneledClientWorldPacket value)
    {
        value = new PacketTunneledClientWorldPacket();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Reliable))
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