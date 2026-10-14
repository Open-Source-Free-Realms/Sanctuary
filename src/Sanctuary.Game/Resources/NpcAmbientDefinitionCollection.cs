using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class NpcAmbientDefinitionCollection : ObservableConcurrentDictionary<string, NpcAmbientDefinition>
{
    private readonly ILogger _logger;

    public NpcAmbientDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool TryGetForModel(string? modelFileName, [MaybeNullWhen(false)] out NpcAmbientDefinition npcAmbientDefinition)
    {
        npcAmbientDefinition = null;
        return !string.IsNullOrEmpty(modelFileName) && TryGetValue(modelFileName.ToLowerInvariant(), out npcAmbientDefinition);
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

            var entries = JsonSerializer.Deserialize<List<NpcAmbientDefinition>>(fileStream, jsonSerializerOptions);

            if (entries is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            Clear();

            foreach (var entry in entries)
            {
                if (!TryAdd(entry.ModelFileName.ToLowerInvariant(), entry))
                {
                    _logger.LogWarning("Failed to add entry. {model} \"{file}\"", entry.ModelFileName, filePath);
                    continue;
                }
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
