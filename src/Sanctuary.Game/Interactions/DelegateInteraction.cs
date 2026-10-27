using System;

using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

/// <summary>
/// An interaction whose behaviour is a delegate. Created by an <see cref="IInteractionProvider"/>,
/// never discovered on its own.
/// </summary>
public sealed class DelegateInteraction : IInteraction
{
    private readonly Action<Player, IEntity> _onInteract;

    public InteractionData Data { get; }

    public int Id => Data.Id;

    public DelegateInteraction(InteractionData data, Action<Player, IEntity> onInteract)
    {
        Data = data;
        _onInteract = onInteract;
    }

    public void OnInteract(Player player, IEntity other) => _onInteract(player, other);
}
