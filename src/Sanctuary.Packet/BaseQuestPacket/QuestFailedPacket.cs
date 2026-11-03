using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class QuestFailedPacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 5;

    public int QuestId;

    public QuestFailedPacket() : base(OpCode)
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
