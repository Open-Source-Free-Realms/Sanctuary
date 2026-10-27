namespace Sanctuary.Database.Entities;

/// <summary>
/// A GM-defined event (e.g. "halloween") that event-flagged world NPCs belong to.
/// </summary>
public class DbWorldEvent
{
    public string Key { get; set; } = null!;

    public bool IsActive { get; set; }
}
