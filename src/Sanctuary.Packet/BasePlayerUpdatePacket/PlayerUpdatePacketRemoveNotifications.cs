using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op35 sub11 — clear overhead/minimap notification entries by guid.
public class PlayerUpdatePacketRemoveNotifications : ISerializablePacket
{
    public const short OpCode = 35;
    public const short SubOpCode = 11;

    public List<ulong> Guids = new();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(Guids.Count);

        foreach (var guid in Guids)
        {
            writer.Write(guid);
            writer.Write(0);
            writer.Write(0);
        }

        return writer.Buffer;
    }
}
