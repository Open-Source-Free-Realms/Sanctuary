using System.Collections.Generic;

namespace Sanctuary.Game.Interactions;

/// <summary>
/// Registers a group of interactions at once, e.g. every entry of a menu.
/// Loaded by <see cref="InteractionManager"/> alongside standalone <see cref="IInteraction"/> types.
/// </summary>
public interface IInteractionProvider
{
    IEnumerable<IInteraction> Interactions { get; }
}
