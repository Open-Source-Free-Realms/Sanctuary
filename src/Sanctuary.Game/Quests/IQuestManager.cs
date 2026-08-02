using System.Numerics;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Quests;

// Data-driven quest engine. Drives the whole quest lifecycle (offer, accept, objective/turn-in,
// complete, abandon, make-active, relog repopulation, world badges) from the quest definitions in
// Quests, so adding a quest is a Quests.json entry rather than code.
// A DI singleton; every method takes the acting player, so it holds no per-player state itself.
public interface IQuestManager
{
    // True if the NPC gives or is a target of any quest (used to wire its interaction).
    bool IsQuestNpc(ulong npcGuid);

    // Player interacted with a quest NPC: turn in an active objective, or offer an available quest.
    void OnNpcInteract(Player player, Npc npc);

    // Player interacted with a Collect-goal pickup (a spawned collectible world object): credit the active
    // Collect goal's count and, at the required count, tick the goal off and advance to the return step.
    void OnCollectInteract(Player player, Npc npc);

    // Player killed an NPC: credit the active Kill goal of any in-progress quest whose
    // KillNpcNameId matches the NPC's NameId.
    void OnNpcKilled(Player player, Npc npc);

    // Player won a battle-instance encounter: complete the active EncounterComplete goal of any
    // in-progress quest whose EncounterId matches.
    // This is what makes a dungeon count as a quest objective.
    void OnEncounterComplete(Player player, int encounterId);

    // Player position changed: complete the active ReachLocation goal of any in-progress quest
    // whose ReachPosition is within ReachRadius (2D). Called from the position-update handler.
    void OnPlayerMoved(Player player);

    // Player accepted a quest offer (QuestReply, Accepted = true).
    void AcceptQuest(Player player, int questId);

    // Player clicked "Complete" on the end screen (QuestEndReply): reward + mark complete.
    void CompleteQuest(Player player, int questId);

    // Player dropped the quest from the journal (CommandPacketQuestAbandon).
    void AbandonQuest(Player player, int questId);

    // Player picked the quest as their tracked quest ("Make Quest Active" / SelectQuest).
    void SetActiveQuest(Player player, int questId);

    // On login, replay the journal/tracker packets for every in-progress quest.
    void RestoreJournal(Player player);

    // Re-push an NPC's world badge to reflect the player's current quest state.
    void RefreshQuestNotification(Player player, ulong npcGuid);

    // Position of the player's currently-tracked quest's target NPC, if any (a spawned turn-in NPC of an
    // in-progress quest). Used to build the "Take Me There" path destination.
    bool TryGetActiveObjectiveTarget(Player player, out Vector3 targetPosition);

    // Re-point (or clear) the tracker arrow / mini-map indicator at the tracked quest's ACTIVE goal
    // target in the player's CURRENT zone.
    void RefreshObjectiveTarget(Player player);
}
