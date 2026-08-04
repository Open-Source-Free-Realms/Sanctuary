using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Traced FUN_00c7cd40 -> FUN_00c7bd30 -> FUN_008fd770. Field semantics beyond QuestId are positional.
public class QuestObjectiveAddedPacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 7;

    public int QuestId; // obj+0xc

    // Body int 0 is the objective identity (row+0xf0) that ObjectiveId must match in later packets.
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
