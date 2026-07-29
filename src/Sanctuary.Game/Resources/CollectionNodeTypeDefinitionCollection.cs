using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public sealed class CollectionNodeTypeDefinitionCollection : ObservableConcurrentDictionary<string, CollectionNodeTypeDefinition>
{
    private readonly ILogger _logger;

    public CollectionNodeTypeDefinitionCollection(ILogger logger)
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
            var entries = JsonSerializer.Deserialize<List<CollectionNodeTypeDefinition>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (entries is null || entries.Count == 0)
                return false;

            var loaded = new Dictionary<string, CollectionNodeTypeDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    _logger.LogError("Collection node type has no key in \"{file}\".", filePath);
                    return false;
                }

                entry.Key = entry.Key.Trim().ToLowerInvariant();

                var totalDropWeight = entry.DropTable.Sum(drop => (long)drop.Weight);

                if (entry.ModelId <= 0 || entry.DropTable.Count == 0 ||
                    entry.DropTable.Any(drop => drop.ItemDefinitionId <= 0 || drop.Weight <= 0) ||
                    totalDropWeight > int.MaxValue ||
                    entry.DropTable.Select(drop => drop.ItemDefinitionId).Distinct().Count() != entry.DropTable.Count ||
                    !loaded.TryAdd(entry.Key, entry))
                {
                    _logger.LogError("Invalid or duplicate collection node type in \"{file}\".", filePath);
                    return false;
                }
            }

            Clear();

            foreach (var entry in loaded)
                TryAdd(entry.Key, entry.Value);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }
    }
}
