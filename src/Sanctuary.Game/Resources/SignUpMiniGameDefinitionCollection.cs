using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Core.IO;
using Sanctuary.Game.MiniGames;

namespace Sanctuary.Game.Resources;

public class SignUpMiniGameDefinitionCollection : ObservableConcurrentDictionary<int, MiniGameUiDefinition>
{
    private readonly ILogger _logger;

    public SignUpMiniGameDefinitionCollection(ILogger logger)
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
                PropertyNameCaseInsensitive = true,
                IncludeFields = true,
                Converters = { new Vector4JsonConverter(), new QuaternionJsonConverter(), new RewardBundleJsonConverter() }
            };

            var entries = JsonSerializer.Deserialize<List<MiniGameUiDefinition>>(fileStream, jsonSerializerOptions);

            if (entries is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            Clear();

            foreach (var entry in entries)
            {
                if (!TryAdd(entry.GameId, entry))
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", entry.GameId, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        return true;
    }
}
