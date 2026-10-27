using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientPathReplyPacket : ClientPathBasePacket, ISerializablePacket
{
    public new const byte OpCode = 2;

    public int Type;
    public int Id;

    public List<Vector4> Points = [];

    public ClientPathReplyPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Type);
        writer.Write(Id);

        writer.Write(Points.Count);

        foreach (var point in Points)
            writer.Write(point);

        return writer.Buffer;
    }
}
