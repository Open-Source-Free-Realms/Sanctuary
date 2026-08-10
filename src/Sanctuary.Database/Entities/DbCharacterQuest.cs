namespace Sanctuary.Database.Entities;

public class DbCharacterQuest
{
    public int QuestId { get; set; }

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;

    public bool Completed { get; set; }

    // How many of the quest's goals are done. Goals tick off in order, so 0 = on the first one.
    public int GoalProgress { get; set; }

    // Pickups gathered so far, when the active goal is a Collect goal.
    public int GoalCount { get; set; }

    // The character's tracked quest, i.e. the breadcrumb / "Take Me There" target. At most one row
    // per character is true.
    public bool IsActive { get; set; }
}
