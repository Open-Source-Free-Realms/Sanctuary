namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

// One NPC that credits a talk goal, and the line it says.
public sealed class TalkTarget
{
    public int NpcId { get; set; }

    // 0 = the goal's DialogueId.
    public int DialogueId { get; set; }

    // 0 = the generic caption.
    public int ResponseId { get; set; }
}
