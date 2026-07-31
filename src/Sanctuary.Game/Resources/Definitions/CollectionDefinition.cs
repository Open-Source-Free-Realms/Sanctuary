using System.Collections.Generic;
using System.Linq;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class CollectionDefinition
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int DescriptionId { get; set; }
    public int Type { get; set; }
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
            CategoryId = CategoryId,
            Id = Id,
            DescriptionId = DescriptionId,
            Type = Type,
            IconId = IconId,
            IconTintId = IconTintId,
            HeaderMetadata = HeaderMetadata,
            PlayerGuid = playerGuid,
            RewardMetadata = RewardMetadata
        };

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];

            collection.Entries.Add(new ClientCollectionEntry
            {
                Id = entry.Id,
                DefinitionId = entry.Id,
                Index = index + 1,
                CategoryId = CategoryId,
                NameId = entry.NameId,
                IconId = entry.IconId,
                IconTintId = entry.IconTintId,
                Collected = ownedItemDefinitionIds.Contains(entry.ItemDefinitionId)
            });
        }

        return collection;
    }
}
