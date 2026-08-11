namespace Sanctuary.Database.Entities;

public class DbCharacterQuest
{
    public int QuestId { get; set; }

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;

    public bool Completed { get; set; }

    public int GoalProgress { get; set; }

    public int GoalCount { get; set; }
}
