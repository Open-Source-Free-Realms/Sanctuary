using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Wire format traced AND wire-verified from the client's real deserializer (case 7:
// FUN_00c7cd40 -> FUN_00c7bd30 -> FUN_00c7acd0 for the header + one int, then FUN_008fd770
// for the objective body). The deserializer is handed the FULL packet buffer (header
// included) and reads, in order:
//   short OpCode (obj+4) + int SubOpCode (obj+8)          [6-byte header, via FUN_00c7acd0]
//   int QuestId (obj+0xc)                                 [looked up via FUN_00c7a610]
//   then FUN_008fd770 reads the objective body:
//     int, int, int, bool(1),
//     RewardBundleBase (FUN_008e7930, 18 fixed fields = 69 bytes, identical to QuestAddPacket),
//     int, int, int, int, bool(1), int
// and requires NO bytes to remain. Total = 6 + 4 + 103 = 113 bytes. Earlier guessed layouts
// (28 then 10 bytes) overflowed/underran and were silently rejected, which stalled the quest
// accept flow (objective never added -> conversation never ended -> camera stuck on the giver).
// Field semantics beyond QuestId are positional; the byte layout is authoritative.
public class QuestObjectiveAddedPacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 7;

    public int QuestId; // obj+0xc

    // FUN_008fd770 objective-body leading ints (traced FUN_00bab950/FUN_00baba60):
    //   int 0 = the objective's IDENTITY: stored at row+0xf0 and used as the hash key that
    //           QuestObjectiveActivated/CompletePacket.ObjectiveId must match. Also resolved as
    //           the objective row's name text id, so a per-goal text id serves both purposes.
    //   int 1 = the tracker goal-row display text id (what quest_helper's addObjective renders).
    //   int 2 = the journal "Objectives" sub-line text id (0 -> "<STRING 0 NOT FOUND>").
    public int ObjectiveNameId;        // body int 0 (identity + name)
    public int ObjectiveDescriptionId; // body int 1
    public int ObjectiveField2;        // body int 2

    public QuestObjectiveAddedPacket() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(49) + int SubOpCode(7) = 6-byte header

        writer.Write(QuestId);

        // FUN_008fd770 objective body:
        writer.Write(ObjectiveNameId); // int
        writer.Write(ObjectiveDescriptionId); // int
        writer.Write(ObjectiveField2); // int
        writer.Write(false); // bool

        // RewardBundleBase (FUN_008e7930) - empty (mirrors QuestAddPacket).
        RewardBundleSerializer.Write(writer, 0, 0);

        // trailing objective fields
        writer.Write(0); // int
        writer.Write(0); // int
        writer.Write(0); // int
        writer.Write(0); // int
        writer.Write(false); // bool
        writer.Write(0); // int

        return writer.Buffer;
    }
}
