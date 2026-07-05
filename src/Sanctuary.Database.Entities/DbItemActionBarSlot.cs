namespace Sanctuary.Database.Entities;

public class DbItemActionBarSlot
{
    public ulong CharacterId { get; set; }
    public int ActionBarId { get; set; }
    public int Slot { get; set; }

    public int ItemId { get; set; }
    public ulong ItemCharacterId { get; set; }
    public DbItem Item { get; set; } = null!;

    public DbCharacter Character { get; set; } = null!;
}
