using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketLoginReply : ISerializablePacket
{
    public const short OpCode = 2;

    public bool Success;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Success);

        return writer.Buffer;
    }
}