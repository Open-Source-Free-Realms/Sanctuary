using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game.Resources;

public sealed class RewardTableDefinitionCollection : ObservableConcurrentDictionary<string, RewardTableDefinition>
{
    private readonly ILogger _logger;
    private readonly object _writeLock = new();

    public RewardTableDefinitionCollection(ILogger logger)
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
            var entries = JsonSerializer.Deserialize<List<RewardTableDefinition>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (entries is null || entries.Count == 0)
            {
                _logger.LogError("No reward tables found in \"{file}\".", filePath);
                return false;
            }

            var loaded = new Dictionary<string, RewardTableDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.DropTable.Count == 0 ||
                    entry.DropTable.Any(drop => drop.Weight <= 0) ||
                    entry.DropTable.OfType<CurrencyRewardDropDefinition>().Any(drop => !Enum.IsDefined(drop.CurrencyType)) ||
                    entry.DropTable.OfType<ItemRewardDropDefinition>().Any(drop => drop.Quantity <= 0))
                {
                    _logger.LogError("Invalid reward table in \"{file}\".", filePath);
                    return false;
                }

                entry.Key = entry.Key.Trim().ToLowerInvariant();

                if (!loaded.TryAdd(entry.Key, entry))
                {
                    _logger.LogError("Duplicate reward table {key} in \"{file}\".", entry.Key, filePath);
                    return false;
                }
            }

            lock (_writeLock)
            {
                foreach (var entry in loaded)
                    this[entry.Key] = entry.Value;

                foreach (var key in Keys.Where(key => !loaded.ContainsKey(key)).ToArray())
                    Remove(key);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }
    }
}
