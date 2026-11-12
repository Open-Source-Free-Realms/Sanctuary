using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GatewayCharacterLogout : ISerializablePacket, IDeserializable<GatewayCharacterLogout>
{
    public const byte OpCode = 103;

    public ulong id;

    public ulong Session;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(id);

        writer.Write(Session);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GatewayCharacterLogout value)
    {
        value = new GatewayCharacterLogout();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.id) || !reader.TryRead(out value.Session))
            return false;

        return reader.RemainingLength == 0;
    }
}