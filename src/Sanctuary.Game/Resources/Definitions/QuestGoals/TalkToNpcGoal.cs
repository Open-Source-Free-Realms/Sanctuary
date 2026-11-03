using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class TalkToNpcGoal : QuestGoal
{
    // Every NPC that credits this goal, each with its own line. RequiredCount sets how many are needed.
    public List<TalkTarget> Targets { get; set; } = [];

    public bool IsCounted => RequiredCount > 1;

    public bool CompleteAfterDialogue { get; set; }

    public int TargetNpcId(QuestDefinition quest)
        => AllTalkTargetNpcIds().FirstOrDefault() is var npcId and not 0 ? npcId : quest.TargetNpcId;

    public override GoalProgress CreateProgress() => new TalkProgress();

    public override bool Matches(QuestDefinition quest, QuestEvent questEvent)
        => questEvent is NpcTalkedEvent { Npc: var npc }
            && (IsCounted ? AllTalkTargetNpcIds().Contains(npc.DefinitionId) : TargetNpcId(quest) == npc.DefinitionId);

    public override QuestGoalResult Progress(QuestDefinition quest, QuestEvent questEvent, GoalProgress progress)
    {
        if (!Matches(quest, questEvent))
            return QuestGoalResult.None;

        if (!IsCounted)
            return QuestGoalResult.Completed;

        if (progress is not TalkProgress talkProgress || !talkProgress.TryAdd(((NpcTalkedEvent)questEvent).Npc.DefinitionId))
            return QuestGoalResult.None;

        return talkProgress.Count >= RequiredCount ? QuestGoalResult.Completed : QuestGoalResult.Progressed;
    }

    public IEnumerable<int> AllTalkTargetNpcIds() =>
        Targets.Select(target => target.NpcId).Where(npcId => npcId != 0).Distinct();

    // The authored Dialogue, else this NPC's own line, else DialogueId. Empty = say nothing.
    public override IReadOnlyList<QuestDialogueLine> ConversationFor(int npcId)
    {
        if (Dialogue.Count > 0)
            return Dialogue;

        foreach (var target in Targets)
        {
            if (target.NpcId == npcId && target.DialogueId != 0)
                return [new QuestDialogueLine { TextId = target.DialogueId, ResponseTextId = target.ResponseId }];
        }

        return base.ConversationFor(npcId);
    }
}
