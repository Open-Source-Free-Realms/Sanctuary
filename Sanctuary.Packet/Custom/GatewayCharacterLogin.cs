using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GatewayCharacterLogin : ISerializablePacket, IDeserializable<GatewayCharacterLogin>
{
    public const byte OpCode = 102;

    public ulong Guid;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GatewayCharacterLogin value)
    {
        value = new GatewayCharacterLogin();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}