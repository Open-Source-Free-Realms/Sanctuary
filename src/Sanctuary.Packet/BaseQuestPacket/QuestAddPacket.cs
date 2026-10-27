using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class QuestAddPacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 3;

    public ClientQuest Quest { get; set; } = new();

    // Shows the quest in the tracker (QuestHandler:PopulateHelper).
    public bool ShowInTracker;

    public QuestAddPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Quest.Serialize(writer);

        writer.Write(ShowInTracker);

        return writer.Buffer;
    }
}
