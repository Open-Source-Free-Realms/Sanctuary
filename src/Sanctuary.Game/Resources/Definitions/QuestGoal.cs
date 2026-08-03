using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

// How a QuestGoal is completed. TalkToNpc, Collect and ReachLocation are wired. Kill and
// EncounterComplete have QuestManager methods ready (OnNpcKilled / OnEncounterComplete) but nothing
// calls them yet - this branch has no combat/battle-instance system to hook them into.
public enum QuestGoalType
{
    // Completes when the player interacts with TargetGuid.
    TalkToNpc = 0,

    // Completes when the player comes within ReachRadius of ReachPosition
    // (2D X/Z check, evaluated on every client position update).
    ReachLocation = 1,

    // Completes when the player has gathered RequiredCount pickups.
    Collect = 2,

    // Completes when the player has defeated RequiredCount NPCs whose NameId matches
    // KillNpcNameId. Not wired yet - see the type-level note above.
    Kill = 3,

    // Completes when the player wins the battle-instance encounter whose activity id matches
    // EncounterId. Not wired yet - see the type-level note above.
    EncounterComplete = 4,
}

// One goal (checklist row) within a quest. Each goal becomes a client objective row
// (QuestObjectiveAddedPacket) shown in the quest tracker with a status icon that ticks off when the
// goal's trigger fires (QuestObjectiveCompletePacket). Goals complete in order; the active goal is
// the first one not yet completed, and the quest is ready to hand in once every goal is done.
public class QuestGoal
{
    // Localized text id for the goal row shown in the tracker/journal ("Talk to Shakey").
    public int NameId { get; set; }

    // Optional longer description id shown as the journal "Objectives" sub-line under the goal row
    // ("Shakey should be hanging out in front of the Wildwood Speedway..."); 0 = reuse NameId.
    public int DescriptionId { get; set; }

    // What the goal's NPC says when this goal is completed at them. Currently only shown for the
    // FINAL goal: it becomes the turn-in end screen's speech bubble (so a quest that ends back at
    // the giver shows the giver's closing line, not the intermediate NPC's). 0 = fall back to the
    // quest's TargetDialogueId.
    public int DialogueId { get; set; }

    // How this goal completes.
    public QuestGoalType Type { get; set; } = QuestGoalType.TalkToNpc;

    // For TalkToNpc: the NPC guid the player must interact with. 0 falls back to the quest's
    // TargetGuid (the turn-in NPC).
    public ulong TargetGuid { get; set; }

    // For count goals (Collect/Kill): how many are required. 0 falls back to CollectSpawns.Count
    // (collect them all). The tracker renders "current/required" as the player collects.
    public int RequiredCount { get; set; }

    // For Collect: the model (Models.txt id) each collectible world object uses - e.g. 93 =
    // bw_collectible_mushrooms_01. Spawned as interactable pickups the player clicks.
    public int CollectModelId { get; set; }

    // For Collect: the collectible's hover/name text id (Global.Text).
    public int CollectNameId { get; set; }

    // For Kill: the NameId of the NPCs this goal counts (e.g. 76190 "Tormented Spirit").
    public int KillNpcNameId { get; set; }

    // For Kill: OPTIONAL additional NameIds that also credit this goal — for hunts where several
    // NPC variants share a camp (Bixie Skirmish counts Soldiers, Guardians, and Magi alike). Combined
    // with KillNpcNameId.
    public List<int> KillNpcNameIds { get; set; } = new();

    // All NameIds this Kill goal credits (the single id + the list, whichever are set).
    public IEnumerable<int> AllKillNameIds()
    {
        if (KillNpcNameId != 0)
            yield return KillNpcNameId;
        foreach (var id in KillNpcNameIds)
            if (id != 0 && id != KillNpcNameId)
                yield return id;
    }

    // For EncounterComplete: the activity/encounter id (e.g. 174 = Frostfang Growler arena) that
    // completes this goal when the player wins it.
    public int EncounterId { get; set; }

    // For Collect: world positions ([x, y, z] each) where the collectible
    // pickups spawn. Interacting with one credits the goal; at RequiredCount the goal ticks
    // off and the next goal (the "return" step) activates. Place at least RequiredCount.
    public List<float[]> CollectSpawns { get; set; } = new();

    // For ReachLocation: the world position ([x, y, z]) the player must get near. The check is 2D
    // (X/Z), so the Y only feeds the map pin.
    public float[] ReachPosition { get; set; } = [];

    // For ReachLocation: how close (world units) counts as "arrived". 0 -> default 12.
    public float ReachRadius { get; set; }
}
