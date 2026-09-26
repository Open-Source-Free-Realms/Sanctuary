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

// One NPC that credits a talk goal, and the line it says.
public sealed class TalkTarget
{
    public ulong Guid { get; set; }

    // 0 = the goal's DialogueId.
    public int DialogueId { get; set; }

    // 0 = the generic caption.
    public int ResponseId { get; set; }
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

    // 0 = the quest's TargetGuid, applied by the caller.
    public ulong TargetGuid { get; set; }

    // Every NPC that credits this goal, each with its own line. RequiredCount sets how many are needed.
    public List<TalkTarget> Targets { get; set; } = [];

    public int RequiredCount { get; set; }

    // A CollectionNodeTypes.json key. Reuses the existing collection node system for pickups
    // instead of quests owning their own spawn points.
    public string CollectNodeType { get; set; } = string.Empty;

    // [x, y, z]. The proximity check is 2D, so the Y only feeds the map pin.
    public float[] ReachPosition { get; set; } = [];

    public float ReachRadius { get; set; } = 12f;

    // Hover cursor and click distance for whatever this goal makes clickable: its NPCs, or its
    // collect pickups. Per-goal, since a quest can mix a distant landmark with a close-up pickup.
    public byte CursorId { get; set; } = 17;
    public int InteractRange { get; set; } = 12;

    public bool IsCountedTalk => Type == QuestGoalType.TalkToNpc && RequiredCount > 1;

    public IEnumerable<ulong> AllTalkTargetGuids()
    {
        if (TargetGuid != 0)
            yield return TargetGuid;

        foreach (var target in Targets)
            if (target.Guid != 0 && target.Guid != TargetGuid)
                yield return target.Guid;
    }

    // The authored Dialogue, else this NPC's own line, else DialogueId. Empty = say nothing.
    public IReadOnlyList<QuestDialogueLine> ConversationFor(ulong npcGuid)
    {
        if (Dialogue.Count > 0)
            return Dialogue;

        foreach (var target in Targets)
        {
            if (target.Guid == npcGuid && target.DialogueId != 0)
                return [new QuestDialogueLine { TextId = target.DialogueId, ResponseTextId = target.ResponseId }];
        }

        return DialogueId != 0 ? [new QuestDialogueLine { TextId = DialogueId }] : [];
    }
}
