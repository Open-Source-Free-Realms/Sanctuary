using System.Diagnostics.CodeAnalysis;

using Sanctuary.Game.Interactions;

namespace Sanctuary.Game;

public interface IInteractionManager
{
    bool Load();

    bool TryGet(int id, [MaybeNullWhen(false)] out IInteraction interaction);
}