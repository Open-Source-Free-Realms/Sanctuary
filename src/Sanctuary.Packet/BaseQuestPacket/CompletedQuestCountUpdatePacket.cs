using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CompletedQuestCountUpdatePacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 12;

    public int Count;

    public CompletedQuestCountUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Count);

        return writer.Buffer;
    }
}
