using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources.Definitions.QuestGoals;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class QuestDefinition
{
    public int QuestId { get; set; }

    public int TitleId { get; set; }
    public int DescriptionId { get; set; }
    public int GiverDialogueId { get; set; }

    // The line above the goal rows in the tracker and journal.
    public int ObjectiveDescriptionId { get; set; }

    public int IconId { get; set; }

    public float? CameraDistance { get; set; }

    // In the order they complete. A quest with none is rejected at load.
    public List<QuestGoal> Goals { get; set; } = [];

    public int GiverNpcId { get; set; }

    // The turn-in NPC, and the fallback for a goal that names no NPC of its own.
    public int TargetNpcId { get; set; }

    public int RewardCoins { get; set; }

    // Goes to the player's active profile.
    public int RewardExperience { get; set; }

    // Item definition ids.
    public List<int> RewardItems { get; set; } = [];

    public int RewardCollectionId { get; set; }

    public int? PrerequisiteQuestId { get; set; }
    public int? NextQuestId { get; set; }

    public bool HideObjectiveTarget { get; set; }

    // Quests that block this one while active or completed, for mutually exclusive quests such as the
    // two race-specific introductions. Each lists the other so the check is symmetric.
    public List<int> ExcludesQuestIds { get; set; } = [];

    // Badge on the giver: "!" while available, "?" while in progress.
    public int NotificationAvailable { get; set; } = 2;
    public int NotificationActive { get; set; } = 6;

    // The final goal's line, since a quest can end back at its giver.
    public int TurnInDialogueId =>
        Goals.Count > 0 && Goals[^1].ConversationFor(TargetNpcId) is { Count: > 0 } lines ? lines[^1].TextId : 0;

    // Shared by the offer flow and the giver's badge so the two can't drift.
    public int GoalTargetNpcId(int goalIndex)
        => goalIndex >= 0 && goalIndex < Goals.Count && Goals[goalIndex] is TalkToNpcGoal talkGoal
            ? talkGoal.TargetNpcId(this)
            : TargetNpcId;

    public IEnumerable<int> StateNpcIds()
        => Goals.SelectMany(goal => goal.HideNpcIdsWhenDone.Concat(goal.NpcEffectsUntilDone.Select(effect => effect.NpcId))).Distinct();

    public int StepEnd(int stepStart)
    {
        if (stepStart >= Goals.Count)
            return stepStart;

        var end = stepStart + 1;
        while (end < Goals.Count && Goals[end].WithPrevious)
            end++;

        return end;
    }

    public int StepStartOf(int goalIndex)
    {
        while (goalIndex > 0 && goalIndex < Goals.Count && Goals[goalIndex].WithPrevious)
            goalIndex--;

        return goalIndex;
    }

    private bool IsGoalDoneFor(QuestLog playerQuests, int goalIndex)
    {
        if (!playerQuests.Contains(QuestId))
            return false;

        return !playerQuests.TryGetInProgress(QuestId, out var progress) || progress.IsGoalDone(goalIndex);
    }

    public bool HidesNpcFor(QuestLog playerQuests, int npcId)
    {
        for (var i = 0; i < Goals.Count; i++)
        {
            if (Goals[i].HideNpcIdsWhenDone.Contains(npcId) && IsGoalDoneFor(playerQuests, i))
                return true;
        }

        return false;
    }

    public int NpcEffectIdFor(QuestLog playerQuests, int npcId)
    {
        for (var i = 0; i < Goals.Count; i++)
        {
            if (IsGoalDoneFor(playerQuests, i))
                continue;

            foreach (var effect in Goals[i].NpcEffectsUntilDone)
            {
                if (effect.NpcId == npcId)
                    return effect.EffectId;
            }
        }

        return 0;
    }

    public bool IsWaitingOn(QuestProgress progress, Npc npc)
    {
        if (progress.GoalIndex >= Goals.Count)
            return GoalTargetNpcId(Goals.Count - 1) == npc.DefinitionId;

        foreach (var (goalIndex, goalProgress) in progress.OpenGoals)
        {
            if (Goals[goalIndex].Matches(this, new NpcTalkedEvent(npc))
                && !(goalProgress is TalkProgress talkProgress && talkProgress.HasTalkedTo(npc.DefinitionId)))
                return true;
        }

        return false;
    }

    public bool IsOfferableFor(QuestLog playerQuests)
    {
        if (playerQuests.Contains(QuestId))
            return false;

        // Before the prerequisite, so exclusive branches that share one still lock each other out.
        foreach (var excludedId in ExcludesQuestIds)
            if (playerQuests.Contains(excludedId))
                return false;

        if (PrerequisiteQuestId is { } prerequisiteQuestId)
            return playerQuests.IsCompleted(prerequisiteQuestId);

        return true;
    }
}
