using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GatewayLoginReply : ISerializablePacket, IDeserializable<GatewayLoginReply>
{
    public const byte OpCode = 101;

    public bool Success;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Success);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GatewayLoginReply value)
    {
        value = new GatewayLoginReply();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Success))
            return false;

        return reader.RemainingLength == 0;
    }
}