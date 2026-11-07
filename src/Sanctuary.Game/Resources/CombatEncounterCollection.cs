using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Core.IO;
using Sanctuary.Game.MiniGames;
using Sanctuary.Game.MiniGames.CombatEncounters;

namespace Sanctuary.Game.Resources;

public class CombatEncounterCollection : ObservableConcurrentDictionary<int, CombatEncounter>
{
    private readonly ILogger _logger;

    public CombatEncounterCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogError("Failed to find directory \"{directory}\"", directoryPath);
            return false;
        }

        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            Converters = { new Vector4JsonConverter(), new QuaternionJsonConverter(), new RewardBundleJsonConverter() }
        };

        Clear();

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.json"))
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var entry = JsonSerializer.Deserialize<CombatEncounter>(fileStream, jsonSerializerOptions);

                if (entry is null)
                {
                    _logger.LogError("Failed to parse file \"{file}\".", filePath);
                    return false;
                }

                if (!TryAdd(entry.Definition.GameId, entry))
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", entry.Definition.GameId, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
                return false;
            }
        }

        return true;
    }
}
