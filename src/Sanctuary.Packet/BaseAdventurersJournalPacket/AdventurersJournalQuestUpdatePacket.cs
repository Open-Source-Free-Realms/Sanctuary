using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op209 sub 2 "QuestUpdate" - pushes live changes to the storybook Adventurer's Journal's quest-state map (int Count, then Count x (questId, status)) so a quest ticks over without a relog. Only affects quests registered in a hub via the Info packet (209/1); a no-op for custom quests until they're added there.
public class AdventurersJournalQuestUpdatePacket : BaseAdventurersJournalPacket, ISerializablePacket
{
    public new const short OpCode = 2;

    // Quest id -> journal status code. Serialized as: int Count, then Count x (int questId, int status).
    public Dictionary<int, int> QuestStates = new();

    public AdventurersJournalQuestUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(209) + int SubOpCode(2)

        writer.Write(QuestStates.Count);
        foreach (var (questId, status) in QuestStates)
        {
            writer.Write(questId);
            writer.Write(status);
        }

        return writer.Buffer;
    }
}
