using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class QuestObjectiveFailedPacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 11;

    public int QuestId;
    public int ObjectiveId;

    public QuestObjectiveFailedPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);
        writer.Write(ObjectiveId);

        return writer.Buffer;
    }
}
