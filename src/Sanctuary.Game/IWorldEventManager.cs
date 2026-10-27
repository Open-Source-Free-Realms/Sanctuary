using System.Collections.Generic;

namespace Sanctuary.Game;

/// <summary>
/// GM-defined events (e.g. "halloween"). World NPCs flagged with an event only spawn while it is active.
/// </summary>
public interface IWorldEventManager
{
    bool IsActive(string key);

    /// <summary>
    /// Every known event key and whether it is active.
    /// </summary>
    IReadOnlyDictionary<string, bool> GetAll();

    /// <summary>
    /// Persists the event's state and spawns or despawns its NPCs in every loaded world zone.
    /// </summary>
    void SetActive(string key, bool active);
}
