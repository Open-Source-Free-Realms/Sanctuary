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
    public int RewardMetadata { get; set; }
    public List<CollectionEntryDefinition> Entries { get; set; } = [];

    public bool IsStarted(IReadOnlySet<int> ownedItemDefinitionIds)
    {
        return Entries.Any(entry => ownedItemDefinitionIds.Contains(entry.ItemDefinitionId));
    }

    public ClientCollection CreateClientCollection(ulong playerGuid, IReadOnlySet<int> ownedItemDefinitionIds)
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
            RewardMetadata = RewardMetadata
        };

        for (var index = 0; index < Entries.Count; index++)
        {
            collection.Entries.Add(CreateClientCollectionEntry(
                Entries[index], index, ownedItemDefinitionIds.Contains(Entries[index].ItemDefinitionId)));
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
}
