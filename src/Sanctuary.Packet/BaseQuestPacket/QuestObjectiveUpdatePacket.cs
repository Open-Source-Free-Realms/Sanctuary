using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class QuestObjectiveUpdatePacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 9;

    public int QuestId;
    public int ObjectiveId;
    public int CurrentCount;
    public float CompletedPercentage;
    public bool Silent;

    // 1, 3, 10, 11, 30, 40 or 51 make this update replace a queued one for the same objective.
    public int Unknown6;

    public QuestObjectiveUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);
        writer.Write(ObjectiveId);
        writer.Write(CurrentCount);
        writer.Write(CompletedPercentage);
        writer.Write(Silent);
        writer.Write(Unknown6);

        return writer.Buffer;
    }
}
