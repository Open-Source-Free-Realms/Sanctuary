using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public class DbCharacterQuest
{
    public int QuestId { get; set; }

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;

    public bool Completed { get; set; }

    public int CurrentGoalId { get; set; }

    public ICollection<DbCharacterQuestProgress> Progress { get; set; } = new HashSet<DbCharacterQuestProgress>();
}
