namespace Sanctuary.Scripting;

/// <summary>
/// What GM-authored scripts can see and do with a player (see <see cref="ScriptSandbox"/>).
/// </summary>
public interface IScriptablePlayer
{
    ulong Guid { get; }
    string Name { get; }
    (float x, float y, float z) Position { get; }

    /// <summary>
    /// Sends a system chat line to this player only.
    /// </summary>
    void Message(string message);

    void GiveItem(int itemDefinitionId, int quantity);
}
