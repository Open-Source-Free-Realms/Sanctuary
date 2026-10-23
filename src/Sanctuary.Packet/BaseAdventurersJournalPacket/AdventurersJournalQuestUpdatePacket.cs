using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class AdventurersJournalQuestUpdatePacket : BaseAdventurersJournalPacket, ISerializablePacket
{
    public new const short OpCode = 2;

    public Dictionary<int, int> QuestStates = new();

    public AdventurersJournalQuestUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestStates.Count);
        foreach (var (questId, status) in QuestStates)
        {
            writer.Write(questId);
            writer.Write(status);
        }

        return writer.Buffer;
    }
}
