using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketItemDefinitions : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 37;

    public byte[] Payload = Array.Empty<byte>();

    public PlayerUpdatePacketItemDefinitions() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.WritePayload(Payload);

        return writer.Buffer;
    }
}