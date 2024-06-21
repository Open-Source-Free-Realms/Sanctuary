using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ServerListReply : ISerializablePacket
{
    public const byte OpCode = 14;

    public List<ClientGameServerData> Servers = new();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Servers);

        return writer.Buffer;
    }
}