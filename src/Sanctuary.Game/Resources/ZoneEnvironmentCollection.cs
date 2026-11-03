using System;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.Resources;

public class ZoneEnvironmentCollection : ObservableConcurrentDictionary<string, ZoneEnvironmentDefinition>
{
    private readonly ILogger _logger;

    public ZoneEnvironmentCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogInformation("Zone environment directory \"{directory}\" not found.", directoryPath);
            return true;
        }

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.json"))
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var jsonSerializerOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var entry = JsonSerializer.Deserialize<ZoneEnvironmentDefinition>(fileStream, jsonSerializerOptions);

                if (entry is null)
                {
                    _logger.LogError("Failed to parse file \"{file}\".", filePath);
                    return false;
                }

                this[Path.GetFileNameWithoutExtension(filePath)] = entry;
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
