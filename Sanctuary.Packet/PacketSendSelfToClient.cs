using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketSendSelfToClient : ISerializablePacket
{
    public const short OpCode = 12;

    public byte[] Payload = Array.Empty<byte>();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.WritePayload(Payload);

        return writer.Buffer;
    }
}