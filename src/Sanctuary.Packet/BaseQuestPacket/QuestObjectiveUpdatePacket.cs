using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: animates the tracker's "1/8, 2/8, ..." count (op49 sub 9).
public class QuestObjectiveUpdatePacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 9;

    public int QuestId;               // +0xc  (matched against quest+0x190)
    public int ObjectiveId;           // +0x10 (the goal identity = QuestObjectiveAddedPacket body int0)
    public int CurrentCount;          // +0x14 -> objective row+0xd4 (current progress)
    public float CompletedPercentage; // +0x18 -> quest+0x28 (overall progress; must be a real float, not NaN)
    public bool Unknown5;             // +0x1c
    public int Unknown6;              // +0x20

    public QuestObjectiveUpdatePacket() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(49) + int SubOpCode(9) = 6-byte header

        writer.Write(QuestId);
        writer.Write(ObjectiveId);
        writer.Write(CurrentCount);
        writer.Write(CompletedPercentage);
        writer.Write(Unknown5);
        writer.Write(Unknown6);

        return writer.Buffer;
    }
}
