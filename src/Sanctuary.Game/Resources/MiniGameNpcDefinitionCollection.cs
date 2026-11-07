using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class MiniGameNpcDefinitionCollection : ObservableConcurrentDictionary<int, MiniGameNpcDefinition>
{
    private readonly ILogger _logger;

    public MiniGameNpcDefinitionCollection(ILogger logger)
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
                Converters = { new JsonStringEnumConverter() }
            };

            var entries = JsonSerializer.Deserialize<List<MiniGameNpcDefinition>>(fileStream, jsonSerializerOptions);

            if (entries is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            foreach (var entry in entries)
            {
                if (!TryAdd(entry.NameId, entry))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", entry.NameId, filePath);
                    continue;
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
}