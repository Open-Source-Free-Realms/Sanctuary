using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public sealed class ClientUpdatePacketCollectionStart : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 8;

    public byte[] Payload = Array.Empty<byte>();

    public ClientUpdatePacketCollectionStart() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.WritePayload(Payload);

        return writer.Buffer;
    }
}
