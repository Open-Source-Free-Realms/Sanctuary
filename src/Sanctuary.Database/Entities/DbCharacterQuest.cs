namespace Sanctuary.Database.Entities;

public class DbCharacterQuest
{
    public int QuestId { get; set; }

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;

    public bool Completed { get; set; }

    // Number of the quest's goals completed so far (goals tick off in order). 0 = on the first goal.
    // Lets multi-goal progress survive relog; single-goal quests only ever hit 0 -> turn-in.
    public int GoalProgress { get; set; }

    // In-progress count for the ACTIVE goal when it's a Collect goal (how many pickups gathered so far,
    // 0..RequiredCount). Lets a partially-collected goal resume after relog instead of restarting at 0.
    // 0 for non-collect goals.
    public int GoalCount { get; set; }

    // Whether this is the character's tracked quest (the arrow/breadcrumb + "Take Me There" target). At
    // most one row per character is true. Without this, ActiveQuestId reset to 0 on every relog, so with
    // 2+ quests active the tracker silently fell back to whichever quest happened to be first.
    public bool IsActive { get; set; }
}
