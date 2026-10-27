using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Interactions;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Quests;

public interface IQuestManager
{
    bool IsQuestNpc(int npcId);

    List<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc);

    void OnEvent(Player player, QuestEvent questEvent);

    void AcceptQuest(Player player, int questId);

    bool CompleteQuest(Player player, int questId);

    bool OfferNextQuest(Player player, int questId);

    void AbandonQuest(Player player, int questId);

    bool ResetQuest(Player player, int questId);

    void SetActiveQuest(Player player, int questId);

    ClientQuestData GetClientQuestData(Player player);

    void SendActiveObjectives(Player player);

    void RestoreJournal(Player player);

    NotificationInfo? GetQuestBadge(Player player, Npc npc);

    bool IsHiddenByQuest(Player player, Npc npc);

    int GetQuestEffectId(Player player, Npc npc);

    void RefreshQuestNotification(Player player, ulong npcGuid);

    void RefreshObjectiveTarget(Player player);
}
