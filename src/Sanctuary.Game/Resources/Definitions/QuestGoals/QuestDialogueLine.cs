using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class QuestDialogueLine
{
    public int TextId { get; set; }

    // 0 = the generic caption.
    public int ResponseTextId { get; set; }

    public bool Closable { get; set; } = true;

    public List<QuestDialogueChoice> Choices { get; set; } = [];
}
