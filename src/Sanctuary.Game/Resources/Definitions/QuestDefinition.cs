using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

// A quest, loaded from Resources/Quests.json. Everything the old hardcoded IntroduceYourselfQuest
// constants held now lives here, so a new quest is a JSON entry instead of new code. Text ids are SOE
// T4 localization ids (resolved client-side as "Global.Text.<id>").
public class QuestDefinition
{
    public int QuestId { get; set; }

    // Localized text ids.
    public int TitleId { get; set; }                // quest title
    public int DescriptionId { get; set; }          // journal "Details" description (tag-free)
    public int GiverDialogueId { get; set; }        // the giver's spoken dialogue (offer flavor)
    public int ObjectiveDescriptionId { get; set; } // main goal text ("Talk to X in Y") - legacy single-goal
    public int SubGoalId { get; set; }              // sub-goal text
    public int TargetDialogueId { get; set; }       // the target's dialogue shown at turn-in
    public int IconId { get; set; }                 // quest icon

    // Ordered checklist of goals shown in the quest tracker (each a client objective row with a
    // tick-off status icon). Empty = the legacy single-goal shape: one goal synthesized from
    // ObjectiveDescriptionId that completes when the player talks to TargetGuid.
    // See EffectiveGoals.
    public List<QuestGoal> Goals { get; set; } = new();

    // NPCs.
    public ulong GiverGuid { get; set; }            // NPC that offers the quest
    public ulong TargetGuid { get; set; }           // NPC the player talks to / turns in at

    // Reward (RewardBundleBase coins at +0x50).
    public int RewardCoins { get; set; }

    // Job/profile experience (XP) granted on completion and shown in the reward preview
    // (RewardBundleBase +0x48). Awarded to the player's active profile via Player.AwardXp.
    public int RewardExperience { get; set; }

    // Item definition ids granted (added to the bags) on completion - e.g. a boombox and a
    // food whose ActivatableAbilityId is a Transformations entry. Empty = no item rewards.
    public List<int> RewardItems { get; set; } = new();

    // Chain / gating.
    public int PrerequisiteQuestId { get; set; }    // 0 = none; must be completed before this is offered
    public int NextQuestId { get; set; }            // 0 = none; becomes offerable once this completes

    // Other quest ids that block this one while active or completed - e.g. the two race-specific
    // "Introduce Yourself" quests, where a player only ever gets one. List both ways. Abandoning
    // clears player.Quests, so the exclusion lifts on its own - no separate reset needed.
    public List<int> ExcludesQuestIds { get; set; } = new();

    // World notification-badge icon ids.
    public int NotificationAvailable { get; set; } = 2; // "!" exclamation (quest available)
    public int NotificationActive { get; set; } = 6;    // "?" question mark (quest in progress / turn-in)

    // The goals to drive the tracker and progression: the authored Goals if any,
    // otherwise a single synthesized "talk to the target NPC" goal built from the legacy
    // ObjectiveDescriptionId/TargetGuid fields so existing quests are
    // unchanged. Never empty for a well-formed quest.
    public IReadOnlyList<QuestGoal> EffectiveGoals =>
        Goals.Count > 0
            ? Goals
            : new[]
            {
                new QuestGoal
                {
                    // The goal row (tracker checklist item) shows the short sub-goal ("Talk to Ricky
                    // Danger"); the objective line ("Introduce yourself to X in Y") is the tracker header.
                    NameId = SubGoalId != 0 ? SubGoalId : ObjectiveDescriptionId,
                    DescriptionId = ObjectiveDescriptionId,
                    DialogueId = TargetDialogueId,
                    Type = QuestGoalType.TalkToNpc,
                    TargetGuid = TargetGuid,
                }
            };

    // The turn-in speech-bubble dialogue: the FINAL goal's NPC reply (multi-goal quests end at the
    // final goal's NPC, e.g. back at the giver), falling back to the legacy single-target
    // TargetDialogueId when the goal doesn't set one.
    public int TurnInDialogueId
    {
        get
        {
            var goals = EffectiveGoals;
            var last = goals[goals.Count - 1];
            return last.DialogueId != 0 ? last.DialogueId : TargetDialogueId;
        }
    }

    // Whether this quest can currently be offered to a player, given their quest state
    // (questId -> completed). Offerable = not already accepted/completed and the prerequisite
    // (if any) is completed. Shared by the offer flow and the giver's "!" badge so they can't drift.
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
