using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources;

public sealed class CollectionDefinitionCollection : ObservableConcurrentDictionary<int, CollectionDefinition>
{
    private readonly ILogger _logger;

    public CollectionDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Failed to find file \"{file}\"", filePath);
            return false;
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var entries = JsonSerializer.Deserialize<List<CollectionDefinition>>(fileStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (entries is null || entries.Count == 0)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            if (!Validate(entries, filePath))
                return false;

            Clear();

            foreach (var entry in entries)
                TryAdd(entry.Id, entry);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }
    }

    public List<ClientCollection> CreateClientCollections(ulong playerGuid, IReadOnlySet<int> ownedItemDefinitionIds,
        IReadOnlySet<int>? collectedEntryIds = null)
    {
        // The client derives its collection data-source width from every definition,
        // then hides unstarted collections whose entries are all uncollected.
        return Values
            .OrderBy(definition => definition.CategoryId)
            .ThenBy(definition => definition.Id)
            .Select(definition => definition.CreateClientCollection(playerGuid, ownedItemDefinitionIds, collectedEntryIds))
            .ToList();
    }

    private bool Validate(List<CollectionDefinition> definitions, string filePath)
    {
        if (definitions.Select(definition => definition.Id).Distinct().Count() != definitions.Count)
        {
            _logger.LogError("Duplicate collection ids found in \"{file}\".", filePath);
            return false;
        }

        var entries = definitions.SelectMany(definition => definition.Entries).ToArray();
        var itemDefinitionIds = entries
            .Where(entry => entry.ItemDefinitionId > 0)
            .Select(entry => entry.ItemDefinitionId)
            .ToArray();

        // Entry ids back direct progress, and item ids are resolved to a collection globally.
        if (entries.Any(entry => entry.Id <= 0 || entry.ItemDefinitionId < 0) ||
            entries.Select(entry => entry.Id).Distinct().Count() != entries.Length ||
            itemDefinitionIds.Distinct().Count() != itemDefinitionIds.Length)
        {
            _logger.LogError("Invalid or duplicate collection entry or item definition ids found in \"{file}\".", filePath);
            return false;
        }

        foreach (var definition in definitions)
        {
            if (definition.Id <= 0 || definition.NameId <= 0 || definition.CategoryId <= 0 ||
                definition.Entries.Count == 0)
            {
                _logger.LogError("Collection {id} has invalid required data in \"{file}\".", definition.Id, filePath);
                return false;
            }
        }

        return true;
    }
}
