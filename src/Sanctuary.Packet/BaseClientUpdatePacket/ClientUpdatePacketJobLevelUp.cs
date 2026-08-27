using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientUpdatePacketJobLevelUp : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 15;

    public byte[] Payload = Array.Empty<byte>();

    public ClientUpdatePacketJobLevelUp() : base(OpCode)
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
