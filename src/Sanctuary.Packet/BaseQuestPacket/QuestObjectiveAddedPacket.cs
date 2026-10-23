using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class QuestObjectiveAddedPacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 7;

    public int QuestId;

    public QuestObjectiveInfo Objective { get; set; } = new();

    public QuestObjectiveAddedPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);

        Objective.Serialize(writer);

        return writer.Buffer;
    }
}
