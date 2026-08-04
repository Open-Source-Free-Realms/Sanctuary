using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: marks a quest complete. Just QuestId, no trailing bool (unlike QuestAbandonedPacket).
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
