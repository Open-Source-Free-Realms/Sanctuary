using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class QuestDialogueChoice
{
    public int ResponseTextId { get; set; }

    public bool Retry { get; set; }

    public List<QuestDialogueLine> Then { get; set; } = [];
}
