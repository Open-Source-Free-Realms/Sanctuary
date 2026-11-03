using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class QuestObjectiveActivatedPacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 8;

    public int QuestId;
    public int ObjectiveId;
    public int RequiredCount;

    // Never read by the client.
    public bool Unknown2;

    public QuestObjectiveActivatedPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);
        writer.Write(ObjectiveId);
        writer.Write(RequiredCount);
        writer.Write(Unknown2);

        return writer.Buffer;
    }
}
