using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketMOTD : ISerializablePacket
{
    public const short OpCode = 87;

    public string Unknown = null!;
    public string Unknown2 = null!;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Unknown);
        writer.Write(Unknown2);

        return writer.Buffer;
    }
}