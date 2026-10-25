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

    private int GoalsDoneFor(QuestLog playerQuests)
    {
        if (!playerQuests.Contains(QuestId))
            return 0;

        return playerQuests.TryGetInProgress(QuestId, out var progress) ? progress.GoalIndex : Goals.Count;
    }

    public bool HidesNpcFor(QuestLog playerQuests, int npcId)
    {
        var goalsDone = GoalsDoneFor(playerQuests);

        for (var i = 0; i < goalsDone; i++)
        {
            if (Goals[i].HideNpcIdsWhenDone.Contains(npcId))
                return true;
        }

        return false;
    }

    public int NpcEffectIdFor(QuestLog playerQuests, int npcId)
    {
        for (var i = GoalsDoneFor(playerQuests); i < Goals.Count; i++)
        {
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

        return Goals[progress.GoalIndex].Matches(this, new NpcTalkedEvent(npc))
            && !(progress.Goal is TalkProgress talkProgress && talkProgress.HasTalkedTo(npc.DefinitionId));
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
