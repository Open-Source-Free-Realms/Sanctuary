using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

// A quest, loaded from Resources/Quests.json. Text ids are SOE T4 localization ids (resolved
// client-side as "Global.Text.<id>").
public class QuestDefinition
{
    public int QuestId { get; set; }

    // Localized text ids.
    public int TitleId { get; set; }
    public int DescriptionId { get; set; }          // journal "Details" description (tag-free)
    public int GiverDialogueId { get; set; }        // the giver's spoken dialogue (offer flavor)
    public int ObjectiveDescriptionId { get; set; } // main goal text ("Talk to X in Y") - legacy single-goal
    public int SubGoalId { get; set; }
    public int TargetDialogueId { get; set; }       // the target's dialogue shown at turn-in
    public int IconId { get; set; }

    // Ordered tracker checklist; empty = legacy single-goal quest synthesized by EffectiveGoals.
    public List<QuestGoal> Goals { get; set; } = new();

    // NPCs.
    public ulong GiverGuid { get; set; }            // NPC that offers the quest
    public ulong TargetGuid { get; set; }           // NPC the player talks to / turns in at

    // Reward (RewardBundleBase coins at +0x50).
    public int RewardCoins { get; set; }

    // Job/profile XP granted on completion (RewardBundleBase +0x48), awarded via Player.AwardXp.
    public int RewardExperience { get; set; }

    // Item definition ids granted on completion (added to the bags); empty = no item rewards.
    public List<int> RewardItems { get; set; } = new();

    // Chain / gating.
    public int PrerequisiteQuestId { get; set; }    // 0 = none; must be completed before this is offered
    public int NextQuestId { get; set; }            // 0 = none; becomes offerable once this completes

    // Mutually-exclusive quest ids while active/completed (list both ways); abandoning auto-lifts it.
    public List<int> ExcludesQuestIds { get; set; } = new();

    // World notification-badge icon ids.
    public int NotificationAvailable { get; set; } = 2; // "!" exclamation (quest available)
    public int NotificationActive { get; set; } = 6;    // "?" question mark (quest in progress / turn-in)

    // Authored Goals if any, else a single synthesized "talk to target NPC" goal from the legacy fields.
    public IReadOnlyList<QuestGoal> EffectiveGoals =>
        Goals.Count > 0
            ? Goals
            : new[]
            {
                new QuestGoal
                {
                    // Goal row shows the short sub-goal text; ObjectiveDescriptionId is the tracker header line.
                    NameId = SubGoalId != 0 ? SubGoalId : ObjectiveDescriptionId,
                    DescriptionId = ObjectiveDescriptionId,
                    DialogueId = TargetDialogueId,
                    Type = QuestGoalType.TalkToNpc,
                    TargetGuid = TargetGuid,
                }
            };

    // Turn-in speech bubble: the final goal's NPC reply, falling back to TargetDialogueId.
    public int TurnInDialogueId
    {
        get
        {
            var goals = EffectiveGoals;
            var last = goals[goals.Count - 1];
            return last.DialogueId != 0 ? last.DialogueId : TargetDialogueId;
        }
    }

    // Offerable = not already accepted/completed, prerequisite (if any) done, and not excluded.
    public bool IsOfferableFor(IReadOnlyDictionary<int, bool> playerQuests)
    {
        if (playerQuests.ContainsKey(QuestId))
            return false; // already accepted or completed

        if (PrerequisiteQuestId != 0)
            return playerQuests.TryGetValue(PrerequisiteQuestId, out var prerequisiteDone) && prerequisiteDone;

        foreach (var excludedId in ExcludesQuestIds)
            if (playerQuests.ContainsKey(excludedId))
                return false; // active or completed - mutually exclusive with this one

        return true;
    }
}
