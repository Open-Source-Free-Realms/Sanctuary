using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class QuestEndBlockedPacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 16;

    public ulong NpcGuid;
    public int TextId;
    public int QuestId;

    public QuestEndBlockedPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(NpcGuid);
        writer.Write(TextId);
        writer.Write(QuestId);

        return writer.Buffer;
    }
}
