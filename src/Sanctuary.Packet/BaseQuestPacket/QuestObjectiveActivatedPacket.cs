using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Traced/wire-verified FUN_00c7cd40 (case 8) -> FUN_00c7b570 -> FUN_00c7aeb0; 19-byte packet (6-byte
// header + int QuestId, int ObjectiveId, int RequiredCount, bool Unknown2) feeding QuestHandler:UpdateObjective.
public class QuestObjectiveActivatedPacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 8;

    public int QuestId;         // obj+0xc
    // Matched against QuestObjectiveAddedPacket's ObjectiveNameId (row+0xf0) via hash bucket, not an
    // add-order index; on match sets the row's state (+0xd0) to 1 (active).
    public int ObjectiveId;     // obj+0x10
    // Stored at row+0xd8 as the tracker's "required" count in the "current/required" suffix
    // (current = row+0xd4, set by sub-opcode 9). 0 hides the counter; N renders "0/N".
    public int RequiredCount;   // obj+0x14
    public bool Unknown2;       // obj+0x18 (1 byte)

    public QuestObjectiveActivatedPacket() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(49) + int SubOpCode(8) = 6-byte header

        writer.Write(QuestId);
        writer.Write(ObjectiveId);
        writer.Write(RequiredCount);
        writer.Write(Unknown2);

        return writer.Buffer;
    }
}
