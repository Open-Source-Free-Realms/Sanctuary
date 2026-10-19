using System;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Interactions;

public sealed class NpcInteractionOption
{
    public required int IconId { get; init; }

    public required int ButtonTextId { get; init; }

    public int TooltipId { get; init; }

    public required Action<Player> Invoke { get; init; }
}
