using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public enum QuestGoalType
{
    TalkToNpc = 0,
    ReachLocation = 1,
    Collect = 2
}

// One turn of an NPC conversation. The player's reply is the caption on the dialog's response button.
public sealed class QuestDialogueLine
{
    public int TextId { get; set; }

    // 0 = the generic "You got it!".
    public int ResponseTextId { get; set; }
}

// One checklist row within a quest. Goals complete in order; the active goal is the first unfinished one.
public sealed class QuestGoal
{
    public int NameId { get; set; }

    // Journal sub-line under the goal row. 0 = reuse NameId.
    public int DescriptionId { get; set; }

    // What the goal's NPC says when the goal completes at them. 0 = the quest's TargetDialogueId.
    public int DialogueId { get; set; }

    // Multi-turn version of DialogueId. Overrides it when non-empty.
    public List<QuestDialogueLine> Dialogue { get; set; } = [];

    public QuestGoalType Type { get; set; } = QuestGoalType.TalkToNpc;

    // 0 = the quest's TargetGuid.
    public ulong TargetGuid { get; set; }

    // Extra NPCs that also credit this goal, for a counted talk step where several interchangeable
    // NPCs share one tracker row ("Talk to Freewheelers - 0/3"). Set RequiredCount to how many.
    public List<ulong> TargetGuids { get; set; } = [];

    // Per-NPC lines for a counted talk goal, index-aligned with AllTalkTargetGuids().
    public List<int> TargetDialogueIds { get; set; } = [];
    public List<int> TargetResponseIds { get; set; } = [];

    // For Collect and counted talk goals. 0 = CollectSpawns.Count.
    public int RequiredCount { get; set; }

    public int CollectModelId { get; set; }
    public int CollectNameId { get; set; }

    // Hover cursor and click distance for the pickups, same wiring collection nodes use.
    public byte CollectCursorId { get; set; } = 17;
    public int CollectInteractRange { get; set; } = 12;

    // Where the pickups spawn, [x, y, z] each.
    public List<float[]> CollectSpawns { get; set; } = [];

    // For ReachLocation, [x, y, z]. The proximity check is 2D, so the Y only feeds the map pin.
    public float[] ReachPosition { get; set; } = [];

    // 0 = default 12.
    public float ReachRadius { get; set; }

    public bool IsCountedTalk => Type == QuestGoalType.TalkToNpc && RequiredCount > 1;

    // Every NPC that credits this talk goal, in the order the per-target lines align to.
    public IEnumerable<ulong> AllTalkTargetGuids()
    {
        if (TargetGuid != 0)
            yield return TargetGuid;

        foreach (var guid in TargetGuids)
            if (guid != 0 && guid != TargetGuid)
                yield return guid;
    }

    // The conversation the given NPC plays: the authored Dialogue, else that NPC's own line, else the
    // goal's shared DialogueId. Empty = say nothing.
    public IReadOnlyList<QuestDialogueLine> ConversationFor(ulong npcGuid)
    {
        if (Dialogue.Count > 0)
            return Dialogue;

        var index = 0;

        foreach (var guid in AllTalkTargetGuids())
        {
            if (guid == npcGuid && index < TargetDialogueIds.Count && TargetDialogueIds[index] != 0)
            {
                return [new QuestDialogueLine
                {
                    TextId = TargetDialogueIds[index],
                    ResponseTextId = index < TargetResponseIds.Count ? TargetResponseIds[index] : 0
                }];
            }

            index++;
        }

        return DialogueId != 0 ? [new QuestDialogueLine { TextId = DialogueId }] : [];
    }
}
