using System;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.Resources;

public class ZoneDefinitionCollection : ObservableConcurrentDictionary<int, BaseZoneDefinition>
{
    private readonly ILogger _logger;

    public ZoneDefinitionCollection(ILogger logger)
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

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.json"))
        {
            try
            {
                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                using var streamReader = new StreamReader(fileStream);

                var entry = JsonSerializer.Deserialize<BaseZoneDefinition>(streamReader.ReadToEnd());

                if (entry is null)
                {
                    _logger.LogError("Failed to parse file \"{file}\".", filePath);
                    return false;
                }

                if (!TryAdd(entry.Id, entry))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", entry.Id, filePath);
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
                return false;
            }
        }

        if (Count == 0)
        {
            _logger.LogError("No data was loaded.");
            return false;
        }

        return true;
    }
}