using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class EquipItemGoal : QuestGoal
{
    public int ItemDefinitionId { get; set; }

    public override bool Matches(QuestDefinition quest, QuestEvent questEvent)
        => questEvent is ItemEquippedEvent { ItemDefinitionId: var itemDefinitionId }
            && itemDefinitionId == ItemDefinitionId;
}
