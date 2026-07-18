using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public sealed class CollectionNodePoolDefinitionCollection : ObservableConcurrentDictionary<string, CollectionNodePoolDefinition>
{
    private readonly ILogger _logger;
    private readonly object _writeLock = new();
    private string? _filePath;

    public CollectionNodePoolDefinitionCollection(ILogger logger)
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
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var entries = JsonSerializer.Deserialize<List<CollectionNodePoolDefinition>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (entries is null || entries.Count == 0)
            {
                _logger.LogError("No collection node pools found in \"{file}\".", filePath);
                return false;
            }

            var loaded = new Dictionary<string, CollectionNodePoolDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.NodeType) ||
                    entry.ZoneDefinitionId <= 0 || entry.MaxActiveNodes < 0 || entry.RespawnSeconds is < 1 or > 86400)
                {
                    _logger.LogError("Invalid collection node pool in \"{file}\".", filePath);
                    return false;
                }

                entry.Key = entry.Key.Trim().ToLowerInvariant();
                entry.NodeType = entry.NodeType.Trim().ToLowerInvariant();

                if (!loaded.TryAdd(entry.Key, entry))
                {
                    _logger.LogError("Duplicate collection node pool {pool} in \"{file}\".", entry.Key, filePath);
                    return false;
                }
            }

            lock (_writeLock)
            {
                foreach (var entry in loaded)
                    this[entry.Key] = entry.Value;

                foreach (var key in Keys.Where(key => !loaded.ContainsKey(key)).ToArray())
                    Remove(key);

                _filePath = filePath;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }
    }

    public bool TryUpdatePersistent(string key, int maxActiveNodes, int respawnSeconds)
    {
        lock (_writeLock)
        {
            if (!TryGetValue(key, out var definition) || maxActiveNodes < 0 || respawnSeconds is < 1 or > 86400)
                return false;

            var previousMaxActiveNodes = definition.MaxActiveNodes;
            var previousRespawnSeconds = definition.RespawnSeconds;
            definition.MaxActiveNodes = maxActiveNodes;
            definition.RespawnSeconds = respawnSeconds;

            if (Save())
                return true;

            definition.MaxActiveNodes = previousMaxActiveNodes;
            definition.RespawnSeconds = previousRespawnSeconds;
            return false;
        }
    }

    private bool Save()
    {
        if (_filePath is null)
            return false;

        try
        {
            var json = JsonSerializer.Serialize(Values.OrderBy(entry => entry.Key), new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, json + Environment.NewLine);
            File.Move(temporaryPath, _filePath, true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save collection node pools to \"{file}\".", _filePath);
            return false;
        }
    }
}
