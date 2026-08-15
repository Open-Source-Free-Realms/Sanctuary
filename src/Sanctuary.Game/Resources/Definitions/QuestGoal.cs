using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public enum QuestGoalType
{
    TalkToNpc = 0,
    ReachLocation = 1,
    Collect = 2
}

public sealed class QuestDialogueLine
{
    public int TextId { get; set; }

    // 0 = the generic caption.
    public int ResponseTextId { get; set; }
}

// One checklist row within a quest. Goals complete in order.
public sealed class QuestGoal
{
    public int NameId { get; set; }

    // 0 = reuse NameId.
    public int DescriptionId { get; set; }

    // What the goal's NPC says on completion. Dialogue overrides this when set.
    public int DialogueId { get; set; }
    public List<QuestDialogueLine> Dialogue { get; set; } = [];

    public QuestGoalType Type { get; set; } = QuestGoalType.TalkToNpc;

    // 0 = the quest's TargetGuid.
    public ulong TargetGuid { get; set; }

    // Extra NPCs that also credit this goal, for a counted talk step where several interchangeable
    // NPCs share one tracker row. RequiredCount sets how many are needed.
    public List<ulong> TargetGuids { get; set; } = [];

    // Per-NPC lines, aligned with AllTalkTargetGuids().
    public List<int> TargetDialogueIds { get; set; } = [];
    public List<int> TargetResponseIds { get; set; } = [];

    // 0 = CollectSpawns.Count.
    public int RequiredCount { get; set; }

    public int CollectModelId { get; set; }
    public int CollectNameId { get; set; }

    // [x, y, z] each.
    public List<float[]> CollectSpawns { get; set; } = [];

    // [x, y, z]. The proximity check is 2D, so the Y only feeds the map pin.
    public float[] ReachPosition { get; set; } = [];

    // 0 = 12.
    public float ReachRadius { get; set; }

    // Hover cursor and click distance for whatever this goal makes clickable: its NPCs, or its
    // collect pickups. Per-goal, since a quest can mix a distant landmark with a close-up pickup.
    public byte CursorId { get; set; } = 17;
    public int InteractRange { get; set; } = 12;

    public bool IsCountedTalk => Type == QuestGoalType.TalkToNpc && RequiredCount > 1;

    public IEnumerable<ulong> AllTalkTargetGuids()
    {
        if (TargetGuid != 0)
            yield return TargetGuid;

        foreach (var guid in TargetGuids)
            if (guid != 0 && guid != TargetGuid)
                yield return guid;
    }

    // The authored Dialogue, else this NPC's own line, else DialogueId. Empty = say nothing.
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
