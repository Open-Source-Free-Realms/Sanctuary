using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketCheckNameReply : ISerializablePacket
{
    public const short OpCode = 211;

    public string FirstName = null!;
    public string LastName = null!;

    public int Result;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(FirstName);
        writer.Write(LastName);
        writer.Write(Result);

        return writer.Buffer;
    }
}