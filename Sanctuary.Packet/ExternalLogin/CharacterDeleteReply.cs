using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CharacterDeleteReply : ISerializablePacket
{
    public const byte OpCode = 10;

    public ulong EntityKey;

    public int Status;

    public byte[] Payload = Array.Empty<byte>();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(EntityKey);

        writer.Write(Status);

        writer.WritePayload(Payload);

        return writer.Buffer;
    }
}