using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class QuestDefinition
{
    public int QuestId { get; set; }

    // Only to keep Quests.json readable - the client resolves TitleId instead.
    public string? Name { get; set; }

    public int TitleId { get; set; }
    public int DescriptionId { get; set; }
    public int GiverDialogueId { get; set; }
    public int ObjectiveDescriptionId { get; set; }
    public int SubGoalId { get; set; }
    public int TargetDialogueId { get; set; }
    public int IconId { get; set; }

    // Empty = a single goal synthesized from ObjectiveDescriptionId/TargetGuid. See EffectiveGoals.
    public List<QuestGoal> Goals { get; set; } = [];

    public ulong GiverGuid { get; set; }
    public ulong TargetGuid { get; set; }

    public int RewardCoins { get; set; }

    // Granted to the player's active profile on completion.
    public int RewardExperience { get; set; }

    // Item definition ids added to the bags on completion.
    public List<int> RewardItems { get; set; } = [];

    // 0 = none.
    public int PrerequisiteQuestId { get; set; }
    public int NextQuestId { get; set; }

    // Quests that block this one while active or completed - the two race-specific "Introduce Yourself"
    // quests, where a player only ever gets one. List it both ways so the check is symmetric.
    public List<int> ExcludesQuestIds { get; set; } = [];

    // Notification badge icons: "!" when available, "?" while in progress.
    public int NotificationAvailable { get; set; } = 2;
    public int NotificationActive { get; set; } = 6;

    // Hover cursor for the giver / turn-in NPC. 17 is the quest cursor (collection nodes use 18).
    public byte NpcCursorId { get; set; } = 17;

    // How close the player has to be for the client to accept a click on that NPC.
    public int NpcInteractRange { get; set; } = 12;

    // The authored goals, or the legacy single "talk to the target NPC" goal for quests written before
    // Goals existed. Never empty for a well-formed quest.
    public IReadOnlyList<QuestGoal> EffectiveGoals =>
        Goals.Count > 0
            ? Goals
            : [
                new QuestGoal
                {
                    NameId = SubGoalId != 0 ? SubGoalId : ObjectiveDescriptionId,
                    DescriptionId = ObjectiveDescriptionId,
                    DialogueId = TargetDialogueId,
                    Type = QuestGoalType.TalkToNpc,
                    TargetGuid = TargetGuid
                }
            ];

    // The turn-in bubble: the final goal's line, since a multi-goal quest can end back at the giver.
    public int TurnInDialogueId
    {
        get
        {
            var goals = EffectiveGoals;
            var last = goals[goals.Count - 1];

            return last.DialogueId != 0 ? last.DialogueId : TargetDialogueId;
        }
    }

    // Whether this quest can be offered to a player, given their quest state (questId -> completed).
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
