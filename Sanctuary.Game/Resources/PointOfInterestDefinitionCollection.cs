using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Sanctuary.Packet.Common;
using Sanctuary.Core.Collections;

namespace Sanctuary.Game.Resources;

public class PointOfInterestDefinitionCollection : ObservableConcurrentDictionary<int, PointOfInterestDefinition>
{
    private readonly ILogger _logger;

    public PointOfInterestDefinitionCollection(ILogger logger)
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
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var streamReader = new StreamReader(fileStream);

            var entries = JsonSerializer.Deserialize<List<PointOfInterestDefinition>>(streamReader.ReadToEnd());

            if (entries is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            foreach (var entry in entries)
            {
                if (!TryAdd(entry.Id, entry))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", entry.Id, filePath);
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