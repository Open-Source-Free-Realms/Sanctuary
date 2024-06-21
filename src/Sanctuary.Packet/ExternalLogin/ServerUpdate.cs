using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ServerUpdate : ISerializablePacket
{
    public const byte OpCode = 15;

    public ClientGameServerData Server = new();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        Server.Serialize(writer);

        return writer.Buffer;
    }
}