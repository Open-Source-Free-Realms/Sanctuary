using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: updates the CURRENT progress count of an in-progress objective (BaseQuestPacket
// sub-opcode 9, "QuestObjectiveUpdatePacket") so the tracker animates "1/8, 2/8, ...". Used by Collect goals.
// Wire layout traced from the client's real deserializer (case 9: FUN_00c7cd40 -> FUN_00c7b5e0 ->
// FUN_00c7af60), which reads, after the 6-byte header, exactly: int, int, int, FLOAT, bool(1), int and
// requires NO trailing bytes (total 27 bytes). The client then looks the objective up by QuestId+ObjectiveId
// (FUN_00c7a990) and stores CurrentCount at objective row+0xd4 (the "current" half of the
// tracker's current/required counter; "required" at +0xd8 was set once by QuestObjectiveActivatedPacket).
// The float goes to the quest's overall-progress field (+0x28); it must be a valid float (a NaN sets the
// stream error flag and the whole packet is rejected). An earlier layout (int,int,int,bool = 19 bytes)
// under-ran the reader, so the client silently dropped every update and the counter stayed frozen.
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
