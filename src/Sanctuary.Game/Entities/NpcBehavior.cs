namespace Sanctuary.Game.Entities;

/// <summary>
/// Simple movement behaviours a referee can give an NPC at runtime.
/// </summary>
public enum NpcBehavior
{
    /// <summary>
    /// Stays where it is (default).
    /// </summary>
    Stay,

    /// <summary>
    /// Walks after a player while they are in view.
    /// </summary>
    Follow,

    /// <summary>
    /// Walks to random points around its spawn position.
    /// </summary>
    Wander
}
