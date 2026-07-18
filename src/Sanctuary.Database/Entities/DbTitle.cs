namespace Sanctuary.Database.Entities;

public class DbTitle
{
    public int Id { get; set; }

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;
}