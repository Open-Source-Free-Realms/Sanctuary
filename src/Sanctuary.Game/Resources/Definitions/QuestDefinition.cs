using System.Collections.Generic;

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

    // In the order they complete. A quest with none is rejected at load.
    public List<QuestGoal> Goals { get; set; } = [];

    public ulong GiverGuid { get; set; }

    // The turn-in NPC, and the fallback for a goal that names no NPC of its own.
    public ulong TargetGuid { get; set; }

    public int RewardCoins { get; set; }

    // Goes to the player's active profile.
    public int RewardExperience { get; set; }

    // Item definition ids.
    public List<int> RewardItems { get; set; } = [];

    // 0 = none.
    public int PrerequisiteQuestId { get; set; }
    public int NextQuestId { get; set; }

    // Quests that block this one while active or completed, for mutually exclusive quests such as the
    // two race-specific introductions. Each lists the other so the check is symmetric.
    public List<int> ExcludesQuestIds { get; set; } = [];

    // Badge on the giver: "!" while available, "?" while in progress.
    public int NotificationAvailable { get; set; } = 2;
    public int NotificationActive { get; set; } = 6;

    // The final goal's line, since a quest can end back at its giver.
    public int TurnInDialogueId => Goals.Count > 0 ? Goals[^1].DialogueId : 0;

    // Shared by the offer flow and the giver's badge so the two can't drift.
    public bool IsOfferableFor(IReadOnlyDictionary<int, bool> playerQuests)
    {
        if (playerQuests.ContainsKey(QuestId))
            return false;

        if (PrerequisiteQuestId != 0)
            return playerQuests.TryGetValue(PrerequisiteQuestId, out var prerequisiteDone) && prerequisiteDone;

        foreach (var excludedId in ExcludesQuestIds)
            if (playerQuests.ContainsKey(excludedId))
                return false;

        return true;
    }
}
