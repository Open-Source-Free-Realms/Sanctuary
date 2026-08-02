using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: marks a quest fully complete (removes it from the active tracker/objectives
// and updates the Hero's Journal). Wire format traced from the client's real deserializer
// (case 4: FUN_00c7cd40 -> FUN_00c7b420 -> FUN_00c7b230, which calls FUN_00c7acd0 for the leading
// short+int header): after the 6-byte header it reads exactly ONE int (obj+0xc, QuestId) and
// requires no trailing bytes. Entire packet is 10 bytes. An earlier guess appended a bool, making
// 11 bytes, which the client silently rejected (extra byte) - leaving the objective and journal
// entry stuck after turn-in.
public class QuestCompletePacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 4;

    public int QuestId; // obj+0xc

    public QuestCompletePacket() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(49) + int SubOpCode(4) = 6-byte header

        writer.Write(QuestId);

        return writer.Buffer;
    }
}
