using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Wire format traced AND wire-verified from the client's real deserializer (case 8:
// FUN_00c7cd40 -> FUN_00c7b570 -> FUN_00c7aeb0, which calls FUN_00c7acd0 for the leading
// short+int). The deserializer is handed the FULL packet buffer (header included) and reads:
// short (OpCode, obj+4) + int (SubOpCode, obj+8) [= the 6-byte header], then the real payload
// int (obj+0xc) + int (obj+0x10) + int (obj+0x14) + bool/1-byte (obj+0x18), and requires no
// bytes to remain. Entire packet is exactly 19 bytes = 6-byte header + 13-byte payload
// (3 int + 1 bool). obj+0xc is the QuestId (same find-quest lookup pattern); the remaining
// fields feed QuestHandler:UpdateObjective (thunk_FUN_00a8f130). Earlier guessed layouts sent
// too many bytes and were silently rejected, stalling the accept flow (camera stuck).
public class QuestObjectiveActivatedPacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 8;

    public int QuestId;         // obj+0xc
    // Objective identity (traced FUN_00c7a910): the client keeps objectives in a per-quest hash
    // table keyed by row+0xf0 = QuestObjectiveAddedPacket's FIRST body int (ObjectiveNameId). This
    // field is matched against that key (bucket = id & 0xf, then full compare) - it is NOT an
    // add-order index. On match the row's state (+0xd0) is set to 1 (active).
    public int ObjectiveId;     // obj+0x10
    // Stored at row+0xd8 on match = the objective's REQUIRED count, rendered by the tracker as the
    // "current/required" suffix on the goal row (current = row+0xd4, set by sub-opcode 9). Send 0
    // for single-step talk goals so no counter shows; N for collect/kill goals renders "0/N".
    // (Sending a text id here painted "0/94359" on the goal row.)
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
