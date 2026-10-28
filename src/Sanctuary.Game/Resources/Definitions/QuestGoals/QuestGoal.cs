using System.Collections.Generic;
using System.Text.Json.Serialization;

using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

// One checklist row within a quest. Goals complete in order.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(TalkToNpcGoal), 0)]
[JsonDerivedType(typeof(ReachLocationGoal), 1)]
[JsonDerivedType(typeof(CollectGoal), 2)]
public abstract class QuestGoal
{
    public int NameId { get; set; }

    // 0 = reuse NameId.
    public int DescriptionId { get; set; }

    // What the goal's NPC says on completion. Dialogue overrides this when set.
    public int DialogueId { get; set; }
    public List<QuestDialogueLine> Dialogue { get; set; } = [];

    // Hover cursor and click distance for whatever this goal makes clickable: its NPCs, or its
    // collect pickups. Per-goal, since a quest can mix a distant landmark with a close-up pickup.
    public byte CursorId { get; set; } = 17;
    public int InteractRange { get; set; } = 12;

    public int RequiredCount { get; set; }

    public bool WithPrevious { get; set; }

    public List<int> HideNpcIdsWhenDone { get; set; } = [];
    public int HideEffectId { get; set; }
    public int HideDelayMs { get; set; }
    public List<QuestNpcEffect> NpcEffectsUntilDone { get; set; } = [];

    public virtual GoalProgress CreateProgress() => new();

    public abstract bool Matches(QuestDefinition quest, QuestEvent questEvent);

    public virtual QuestGoalResult Progress(QuestDefinition quest, QuestEvent questEvent, GoalProgress progress)
        => Matches(quest, questEvent) ? QuestGoalResult.Completed : QuestGoalResult.None;

    public virtual IReadOnlyList<QuestDialogueLine> ConversationFor(int npcId)
    {
        if (Dialogue.Count > 0)
            return Dialogue;

        return DialogueId != 0 ? [new QuestDialogueLine { TextId = DialogueId }] : [];
    }
}
