using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketInitializationParameters : ISerializablePacket
{
    public const short OpCode = 166;

    public string Environment = null!;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Environment, 64);

        return writer.Buffer;
    }
}