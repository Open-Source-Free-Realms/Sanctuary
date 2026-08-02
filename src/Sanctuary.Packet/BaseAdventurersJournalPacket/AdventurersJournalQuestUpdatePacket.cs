using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// BaseAdventurersJournalPacket (op209) sub-opcode 2 = "QuestUpdate" — pushes live changes to the
// storybook Adventurer's Journal's quest-state map, so a quest ticks over in the journal without a relog.
//
// WIRE FORMAT reverse-engineered (Ghidra, FreeRealms 2014-03-13) from the client's receive path:
//   router FUN_009471a0 case 0xd1 -> op209 dispatcher FUN_00a45d80 -> case (subop==2) -> allocates a
//   0x94-byte SoeUtil::HashListMap<int,int,32,-1> (ctor FUN_00798600) and deserializes it with
//   FUN_00a45bf0 -> FUN_00a45680. FUN_00a45680 reads: int Count, then Count x (int Key, int Value).
//   Key = quest id; Value = that quest's journal status (the same status the Info packet's HubQuest
//   "Unknown" field carries - observed values 1/2/7, a status code, NOT a plain bool).
//
// NOTE: the storybook journal is keyed by the retail hub/region structure sent in the Info packet
// (209/1); a QuestUpdate only visibly changes quests that are registered in a hub there. Our custom
// quests aren't in the retail hubs yet, so this updates retail-hub quests live but is a no-op for
// custom ones until they're added to the Info packet's HubQuests.
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
