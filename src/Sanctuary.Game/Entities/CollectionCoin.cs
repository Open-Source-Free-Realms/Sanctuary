using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Entities;

public sealed class CollectionCoin : Npc
{
    private readonly ConcurrentSet<ulong> _reservations = [];

    public CollectionCoinDefinition Definition { get; }

    public CollectionCoin(IZone zone, CollectionCoinDefinition definition)
        : base(zone)
    {
        Definition = definition;
    }

    public bool TryReserve(ulong playerGuid) => _reservations.TryAdd(playerGuid);

    public void Release(ulong playerGuid) => _reservations.TryRemove(playerGuid);

    public override bool IsVisibleTo(Player player) =>
        !player.CollectedCollectionEntryIds.Contains(Definition.EntryId);
}
