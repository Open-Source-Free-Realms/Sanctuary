using System;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class ZoneNpcsCollection : ObservableConcurrentDictionary<string, ZoneNpcsDefinition>
{
    private readonly ILogger _logger;

    public ZoneNpcsCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return true;

        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true
        };

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.json"))
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var entry = JsonSerializer.Deserialize<ZoneNpcsDefinition>(fileStream, jsonSerializerOptions);

                if (entry is null)
                {
                    _logger.LogError("No entries found in file \"{file}\".", filePath);
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

    public bool AddDefinitionsTo(NpcDefinitionCollection npcs)
    {
        foreach (var (zoneName, zoneNpcs) in this)
        {
            foreach (var definition in zoneNpcs.Definitions)
            {
                if (!npcs.TryAdd(definition.Id, definition))
                {
                    _logger.LogError("Zone {zone} NPC definition {id} is already defined.", zoneName, definition.Id);
                    return false;
                }
            }
        }

        return true;
    }
}
