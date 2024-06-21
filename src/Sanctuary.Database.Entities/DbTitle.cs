namespace Sanctuary.Database.Entities;

public class DbTitle
{
    public int Id { get; set; }

    public ulong CharacterGuid { get; set; }
    public DbCharacter Character { get; set; } = null!;
}