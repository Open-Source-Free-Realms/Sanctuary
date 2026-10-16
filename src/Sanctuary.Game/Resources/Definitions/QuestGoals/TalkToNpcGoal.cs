using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class TalkToNpcGoal : QuestGoal
{
    // Every NPC that credits this goal, each with its own line. RequiredCount sets how many are needed.
    public List<TalkTarget> Targets { get; set; } = [];

    public int RequiredCount { get; set; }

    public bool IsCounted => RequiredCount > 1;

    public IEnumerable<ulong> AllTalkTargetGuids() =>
        Targets.Select(target => target.Guid).Where(guid => guid != 0).Distinct();

    // The authored Dialogue, else this NPC's own line, else DialogueId. Empty = say nothing.
    public override IReadOnlyList<QuestDialogueLine> ConversationFor(ulong npcGuid)
    {
        if (Dialogue.Count > 0)
            return Dialogue;

        foreach (var target in Targets)
        {
            if (target.Guid == npcGuid && target.DialogueId != 0)
                return [new QuestDialogueLine { TextId = target.DialogueId, ResponseTextId = target.ResponseId }];
        }

        return base.ConversationFor(npcGuid);
    }
}
