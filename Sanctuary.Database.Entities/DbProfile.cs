using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

public class DbProfile
{
    public int Id { get; set; }

    public int Level { get; set; }
    public int LevelXP { get; set; }

    public ICollection<DbItem> Items { get; set; } = new HashSet<DbItem>();

    public ulong CharacterGuid { get; set; }
    public DbCharacter Character { get; set; } = null!;
}