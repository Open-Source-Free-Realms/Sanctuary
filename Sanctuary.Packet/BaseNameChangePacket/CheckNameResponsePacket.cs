using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class CheckNameResponsePacket : BaseNameChangePacket, ISerializablePacket
{
    public new const short OpCode = 2;

    public NameData Name = new();

    public int Result;

    public NameChangeType Type;

    public ulong Guid;

    public CheckNameResponsePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Name.Serialize(writer);

        writer.Write(Result);

        writer.Write(Type);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public override string ToString()
    {
        return $"{nameof(Name)}: {Name.FullName}, {nameof(Result)}: {Result}, {nameof(Type)}: {Type}, {nameof(Guid)}: {Guid}";
    }
}