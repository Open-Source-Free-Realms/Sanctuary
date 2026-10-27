using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketMOTD : ISerializablePacket
{
    public const short OpCode = 87;

    public string Title = null!;
    public string Message = null!;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Title);
        writer.Write(Message);

        return writer.Buffer;
    }
}