using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Wire format traced from the client's real deserializer (case 10: FUN_00c7cd40 ->
// FUN_00c7b650 -> FUN_00c7b070, which calls FUN_00c7acd0 for the leading short+int header).
// After the 6-byte header the payload is: int (obj+0xc, QuestId) + int (obj+0x10) +
// float (obj+0x14, NaN-checked) + int (obj+0x18) + bool (obj+0x1c). Entire packet is 23 bytes
// = 6-byte header + 17-byte payload. Marks a quest objective complete (the check-off in the
// tracker/journal). Sent when the completion condition is met (interacting with the target NPC).
public class QuestObjectiveCompletePacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 10;

    public int QuestId;         // obj+0xc
    // Objective identity (traced FUN_00c7aa10): matched against the per-quest objective hash key at
    // row+0xf0 = QuestObjectiveAddedPacket's FIRST body int (ObjectiveNameId) - NOT an add-order
    // index. On match the row's state (+0xd0) is set to 2 (complete) and Percent is written to the
    // quest's CompletedPercentage (+0x28); on miss only the percentage updates (no checkmark).
    public int ObjectiveId;     // obj+0x10
    public float Percent;       // obj+0x14 (NaN-checked; 1.0 = complete)
    public int Unknown;         // obj+0x18
    // obj+0x1c (1 byte). Traced (FUN_00a936e0 gate `if (param_5 == 0)`): this is a SILENT flag, not
    // "completed". When 0, the client shows the "Goal complete!" timed banner (FUN_00cb7070
    // "CompleteObjective", 6s); when 1, the checkmark still ticks (set by the row lookup) but the
    // banner is suppressed. Send 0 on a real completion; 1 when replaying completed goals on relog.
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
