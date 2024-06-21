using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class NameChangeResponsePacket : BaseNameChangePacket, ISerializablePacket
{
    public new const short OpCode = 4;

    public NameChangeType Type;

    public ulong Guid;

    public NameData Name = new();

    public int Result;

    public NameChangeResponsePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Type);

        writer.Write(Guid);

        Name.Serialize(writer);

        writer.Write(Result);

        return writer.Buffer;
    }
}