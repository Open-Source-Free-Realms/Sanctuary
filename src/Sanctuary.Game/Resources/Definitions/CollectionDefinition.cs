using System.Collections.Generic;
using System.Linq;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class CollectionDefinition
{
    public int Id { get; set; }
    public int NameId { get; set; }
    public int CategoryId { get; set; }
    public int DescriptionId { get; set; }
    public int IconId { get; set; }
    public int IconTintId { get; set; }
    public int HeaderMetadata { get; set; }
    public int RewardIconId { get; set; } = 3901;
    public int RewardCurrencyIconId { get; set; } = 2416;
    public RewardBundleEntryType RewardEntryType { get; set; } = RewardBundleEntryType.Experience;
    public int RewardIconId2 { get; set; } = 3901;
    public int RewardEntryTintId { get; set; }
    public int RewardCurrencyIconId2 { get; set; } = 2416;
    public int RewardPoints { get; set; } = 50;
    public int RewardMetadata { get; set; }
    public int RewardEntryItemGuid { get; set; }
    public int Unknown20 { get; set; } = 5604;
    public List<CollectionEntryDefinition> Entries { get; set; } = [];

    public bool IsStarted(IReadOnlySet<int> ownedItemDefinitionIds,
        IReadOnlySet<int>? collectedEntryIds = null)
    {
        return Entries.Any(entry => IsCollected(entry, ownedItemDefinitionIds, collectedEntryIds));
    }

    public bool IsComplete(IReadOnlySet<int> ownedItemDefinitionIds,
        IReadOnlySet<int>? collectedEntryIds = null)
    {
        return Entries.All(entry => IsCollected(entry, ownedItemDefinitionIds, collectedEntryIds));
    }

    public ClientCollection CreateClientCollection(ulong playerGuid, IReadOnlySet<int> ownedItemDefinitionIds,
        IReadOnlySet<int>? collectedEntryIds = null)
    {
        var collection = new ClientCollection
        {
            Id = Id,
            NameId = NameId,
            DescriptionId = DescriptionId,
            CategoryId = CategoryId,
            IconId = IconId,
            IconTintId = IconTintId,
            HeaderMetadata = HeaderMetadata,
            PlayerGuid = playerGuid,
            RewardIconId = RewardIconId,
            RewardCurrencyIconId = RewardCurrencyIconId,
            Unknown13 = (int)RewardEntryType,
            RewardIconId2 = RewardIconId2,
            Unknown15 = RewardEntryTintId,
            RewardCurrencyIconId2 = RewardCurrencyIconId2,
            RewardPoints = RewardPoints,
            RewardMetadata = RewardMetadata,
            RewardEntryItemGuid = RewardEntryItemGuid,
            Unknown20 = Unknown20
        };

        for (var index = 0; index < Entries.Count; index++)
        {
            collection.Entries.Add(CreateClientCollectionEntry(
                Entries[index], index, IsCollected(Entries[index], ownedItemDefinitionIds, collectedEntryIds)));
        }

        return collection;
    }

    public ClientCollectionEntry CreateClientCollectionEntry(CollectionEntryDefinition entry, int index, bool collected)
    {
        return new ClientCollectionEntry
        {
            Id = entry.Id,
            DefinitionId = entry.Id,
            Index = index + 1,
            CollectionId = Id,
            NameId = entry.NameId,
            IconId = entry.IconId,
            IconTintId = entry.IconTintId,
            Collected = collected
        };
    }

    private static bool IsCollected(CollectionEntryDefinition entry, IReadOnlySet<int> ownedItemDefinitionIds,
        IReadOnlySet<int>? collectedEntryIds)
    {
        return entry.ItemDefinitionId > 0
            ? ownedItemDefinitionIds.Contains(entry.ItemDefinitionId)
            : collectedEntryIds?.Contains(entry.Id) == true;
    }
}
