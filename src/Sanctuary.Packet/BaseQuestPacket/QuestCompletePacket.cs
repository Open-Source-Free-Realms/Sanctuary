using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class QuestCompletePacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 4;

    public int QuestId;

    public QuestCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);

        return writer.Buffer;
    }
}
