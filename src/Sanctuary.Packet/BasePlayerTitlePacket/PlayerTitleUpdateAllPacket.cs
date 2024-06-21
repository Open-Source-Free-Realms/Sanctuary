using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerTitleUpdateAllPacket : BasePlayerTitlePacket, ISerializablePacket
{
    public new const short OpCode = 3;

    public byte[] Payload = Array.Empty<byte>();

    public PlayerTitleUpdateAllPacket() : base(OpCode)
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