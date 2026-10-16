using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public sealed class CollectionCoinDefinitionCollection : ObservableConcurrentDictionary<int, CollectionCoinDefinition>
{
    private readonly ILogger _logger;

    public CollectionCoinDefinitionCollection(ILogger logger)
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
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var definitions = JsonSerializer.Deserialize<List<CollectionCoinDefinition>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (definitions is null || definitions.Count == 0 || definitions.Any(definition =>
                    definition.NpcDefinitionId <= 0 || definition.CollectionId <= 0 || definition.EntryId <= 0) ||
                definitions.Select(definition => definition.NpcDefinitionId).Distinct().Count() != definitions.Count ||
                definitions.Select(definition => definition.EntryId).Distinct().Count() != definitions.Count)
            {
                _logger.LogError("Invalid or duplicate collection coin NPC or entry definitions found in \"{file}\".", filePath);
                return false;
            }

            Clear();

            foreach (var definition in definitions)
                TryAdd(definition.NpcDefinitionId, definition);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }
    }
}
