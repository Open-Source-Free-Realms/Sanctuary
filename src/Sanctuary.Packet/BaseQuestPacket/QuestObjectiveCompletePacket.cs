using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Traced FUN_00c7cd40 (case 10) -> FUN_00c7b650 -> FUN_00c7b070; 23-byte packet (6-byte header +
// int QuestId, int ObjectiveId, float Percent, int Unknown, bool Silent). Marks a quest objective complete.
public class QuestObjectiveCompletePacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 10;

    public int QuestId;         // obj+0xc
    // Matched against QuestObjectiveAddedPacket's ObjectiveNameId (row+0xf0), not an add-order index; on
    // match sets row state (+0xd0) to complete and writes Percent to the quest's CompletedPercentage (+0x28).
    public int ObjectiveId;     // obj+0x10
    public float Percent;       // obj+0x14 (NaN-checked; 1.0 = complete)
    public int Unknown;         // obj+0x18
    // SILENT flag, not "completed" (FUN_00a936e0 gate): 0 shows the "Goal complete!" banner, 1 suppresses
    // it while still ticking the checkmark. Send 0 on a real completion, 1 when replaying goals on relog.
    public bool Silent;         // obj+0x1c

    public QuestObjectiveCompletePacket() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(49) + int SubOpCode(10) = 6-byte header

        writer.Write(QuestId);
        writer.Write(ObjectiveId);
        writer.Write(Percent);
        writer.Write(Unknown);
        writer.Write(Silent);

        return writer.Buffer;
    }
}
