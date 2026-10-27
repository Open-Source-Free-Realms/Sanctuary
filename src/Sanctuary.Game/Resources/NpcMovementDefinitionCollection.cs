using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class NpcMovementDefinitionCollection : ObservableConcurrentDictionary<ulong, NpcMovementDefinition>
{
    private readonly ILogger _logger;

    public ConcurrentDictionary<string, NpcMovementDefinition> ByModel { get; } = new(StringComparer.OrdinalIgnoreCase);

    public NpcMovementDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool TryGetForNpc(ulong guid, string? modelFileName, [MaybeNullWhen(false)] out NpcMovementDefinition npcMovementDefinition)
    {
        if (TryGetValue(guid, out npcMovementDefinition))
            return true;

        npcMovementDefinition = null;
        return !string.IsNullOrEmpty(modelFileName) && ByModel.TryGetValue(modelFileName, out npcMovementDefinition);
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

            var entries = JsonSerializer.Deserialize<List<NpcMovementDefinition>>(fileStream, jsonSerializerOptions);

            if (entries is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            Clear();
            ByModel.Clear();

            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.ModelFileName))
                {
                    if (!ByModel.TryAdd(entry.ModelFileName, entry))
                        _logger.LogWarning("Failed to add entry. {model} \"{file}\"", entry.ModelFileName, filePath);

                    continue;
                }

                if (!TryAdd(entry.Guid, entry))
                {
                    _logger.LogWarning("Failed to add entry. {guid} \"{file}\"", entry.Guid, filePath);
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
