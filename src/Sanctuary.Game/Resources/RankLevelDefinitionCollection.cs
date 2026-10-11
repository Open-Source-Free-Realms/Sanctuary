using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class RankLevelDefinitionCollection : ObservableConcurrentDictionary<int, RankLevelDefinition>
{
    private readonly ILogger _logger;

    public RankLevelDefinitionCollection(ILogger logger)
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

            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var list = JsonSerializer.Deserialize<List<RankLevelDefinition>>(fileStream, jsonSerializerOptions);

            if (list is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            foreach (var entry in list)
            {
                if (!TryAdd(entry.Level, entry))
                {
                    _logger.LogWarning("Failed to add entry. {level} \"{file}\"", entry.Level, filePath);
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        if (Count == 0)
        {
            _logger.LogError("No data was loaded. \"{file}\"", filePath);
            return false;
        }

        return true;
    }

    public int GetRankPercent(int level, int levelXp)
    {
        if (!TryGetValue(level, out var rankLevel) || rankLevel.StarsToNextLevel <= 0)
            return 0;

        return (int)(100.0 * levelXp / rankLevel.StarsToNextLevel);
    }
}
