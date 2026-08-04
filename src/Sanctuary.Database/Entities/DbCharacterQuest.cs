namespace Sanctuary.Database.Entities;

public class DbCharacterQuest
{
    public int QuestId { get; set; }

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;

    public bool Completed { get; set; }

    // Index of the goal currently in progress (goals tick off in order); lets multi-goal progress survive relog.
    public int GoalProgress { get; set; }

    // Pickups gathered so far for the active Collect goal (0..RequiredCount); 0 for non-collect goals.
    public int GoalCount { get; set; }

    // Whether this is the character's tracked quest (breadcrumb/"Take Me There" target); at most one row per character is true.
    public bool IsActive { get; set; }
}
