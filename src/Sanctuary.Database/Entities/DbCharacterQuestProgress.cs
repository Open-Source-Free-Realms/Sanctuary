namespace Sanctuary.Database.Entities;

public class DbCharacterQuestProgress
{
    public int QuestId { get; set; }
    public ulong CharacterId { get; set; }
    public DbCharacterQuest Quest { get; set; } = null!;

    public int GoalId { get; set; }

    public ulong TargetId { get; set; }

    public int Count { get; set; }
}
