using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Interactions;

public interface IInteraction
{
    static int UniqueId;

    int Id { get; }

    void OnInteract(Player player, IEntity other);
}