using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

// How a QuestGoal is completed.
public enum QuestGoalType
{
    // Completes when the player interacts with TargetGuid.
    TalkToNpc = 0,

    // Completes when the player comes within ReachRadius of ReachPosition (2D X/Z check).
    ReachLocation = 1,

    // Completes when the player has gathered RequiredCount pickups.
    Collect = 2,
}

// One goal (tracker checklist row) within a quest. Goals complete in order; the quest is ready to
// hand in once every goal is done.
public class QuestGoal
{
    // Localized text id for the goal row shown in the tracker/journal ("Talk to Shakey").
    public int NameId { get; set; }

    // Optional longer description shown as the journal "Objectives" sub-line; 0 = reuse NameId.
    public int DescriptionId { get; set; }

    // Final-goal-only: this goal's turn-in speech-bubble line; 0 falls back to TargetDialogueId.
    public int DialogueId { get; set; }

    // How this goal completes.
    public QuestGoalType Type { get; set; } = QuestGoalType.TalkToNpc;

    // For TalkToNpc: the NPC guid to interact with. 0 falls back to the quest's TargetGuid.
    public ulong TargetGuid { get; set; }

    // For Collect: how many pickups are required. 0 falls back to CollectSpawns.Count (collect them all).
    public int RequiredCount { get; set; }

    // For Collect: the model (Models.txt id) each collectible world object uses.
    public int CollectModelId { get; set; }

    // For Collect: the collectible's hover/name text id (Global.Text).
    public int CollectNameId { get; set; }

    // For Collect: world spawn positions ([x, y, z] each); reaching RequiredCount advances the goal.
    public List<float[]> CollectSpawns { get; set; } = new();

    // For ReachLocation: the world position ([x, y, z]) to get near; check is 2D (X/Z), Y feeds only the map pin.
    public float[] ReachPosition { get; set; } = [];

    // For ReachLocation: how close (world units) counts as "arrived". 0 -> default 12.
    public float ReachRadius { get; set; }
}
