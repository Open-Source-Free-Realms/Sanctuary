using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class TunnelAppPacketServerToClient : ISerializablePacket
{
    public const byte OpCode = 17;

    public int ServerId;

    public byte[] Payload = Array.Empty<byte>();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(ServerId);

        writer.WritePayload(Payload);

        return writer.Buffer;
    }
}