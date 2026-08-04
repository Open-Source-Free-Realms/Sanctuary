using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client (opcode 98, sub 2): reply to ClientPathRequestPacket. Wire format from client deserializer FUN_008faf30. ResultType=1 routes to the breadcrumb follower (FUN_009cd2f0); an empty Path means "path attempt failed".
public class ClientPathReplyPacket : ClientPathBasePacket, ISerializablePacket
{
    public new const byte OpCode = 2;

    // 1 = breadcrumb follower path (the "Take Me There" trail).
    public int ResultType = 1;

    // Echoes the request id.
    public int RequestId;

    // The path waypoints (start -> destination). Each drawn as a green trail node.
    public List<Vector4> Path = new();

    public ClientPathReplyPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // opcode 98 + sub 2

        writer.Write(ResultType);
        writer.Write(RequestId);

        writer.Write(Path.Count);
        foreach (var point in Path)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
            writer.Write(point.Z);
            writer.Write(point.W);
        }

        return writer.Buffer;
    }
}
