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

    public List<ClientCollection> CreateClientCollections(ulong playerGuid, IReadOnlySet<int> ownedItemDefinitionIds)
    {
        return Values
            .Where(definition => definition.IsStarted(ownedItemDefinitionIds))
            .OrderBy(definition => definition.CategoryId)
            .ThenBy(definition => definition.Id)
            .Select(definition => definition.CreateClientCollection(playerGuid, ownedItemDefinitionIds))
            .ToList();
    }

    private bool Validate(List<CollectionDefinition> definitions, string filePath)
    {
        if (definitions.Select(definition => definition.Id).Distinct().Count() != definitions.Count)
        {
            _logger.LogError("Duplicate collection ids found in \"{file}\".", filePath);
            return false;
        }

        foreach (var definition in definitions)
        {
            if (definition.Id <= 0 || definition.CategoryId <= 0 || definition.Entries.Count == 0)
            {
                _logger.LogError("Collection {id} has invalid required data in \"{file}\".", definition.Id, filePath);
                return false;
            }

            if (definition.Entries.Select(entry => entry.Id).Distinct().Count() != definition.Entries.Count ||
                definition.Entries.Any(entry => entry.ItemDefinitionId <= 0) ||
                definition.Entries.Select(entry => entry.ItemDefinitionId).Distinct().Count() != definition.Entries.Count)
            {
                _logger.LogError("Collection {id} has duplicate entry or item definition ids in \"{file}\".", definition.Id, filePath);
                return false;
            }
        }

        return true;
    }
}
