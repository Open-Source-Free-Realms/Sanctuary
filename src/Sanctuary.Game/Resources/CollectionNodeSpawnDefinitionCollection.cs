using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public sealed class CollectionNodeSpawnDefinitionCollection : ObservableConcurrentDictionary<int, CollectionNodeSpawnDefinition>
{
    private readonly ILogger _logger;
    private readonly object _writeLock = new();
    private string? _directoryPath;

    public CollectionNodeSpawnDefinitionCollection(ILogger logger)
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

        try
        {
            var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);

            if (files.Length == 0)
            {
                _logger.LogError("No collection node spawn files found in \"{directory}\".", directoryPath);
                return false;
            }

            var loaded = new Dictionary<int, CollectionNodeSpawnDefinition>();

            foreach (var filePath in files.Order())
            {
                if (!int.TryParse(Path.GetFileNameWithoutExtension(filePath), out var zoneDefinitionId) ||
                    zoneDefinitionId <= 0)
                {
                    _logger.LogError("Collection node spawn file \"{file}\" is not named for a valid zone id.", filePath);
                    return false;
                }

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var entries = JsonSerializer.Deserialize<List<CollectionNodeSpawnDefinition>>(stream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (entries is null || entries.Any(entry => entry.Id <= 0 || string.IsNullOrWhiteSpace(entry.Pool) ||
                    entry.Position.Length != 3 || entry.Position.Any(value => !float.IsFinite(value)) ||
                    !float.IsFinite(entry.Heading)))
                {
                    _logger.LogError("Invalid collection node spawns in \"{file}\".", filePath);
                    return false;
                }

                foreach (var entry in entries)
                {
                    entry.Pool = entry.Pool.Trim().ToLowerInvariant();
                    entry.ZoneDefinitionId = zoneDefinitionId;

                    if (!loaded.TryAdd(entry.Id, entry))
                    {
                        _logger.LogError("Duplicate collection node spawn id {id} in \"{file}\".", entry.Id, filePath);
                        return false;
                    }
                }
            }

            lock (_writeLock)
            {
                foreach (var entry in loaded)
                    this[entry.Key] = entry.Value;

                foreach (var id in Keys.Where(id => !loaded.ContainsKey(id)).ToArray())
                    Remove(id);

                _directoryPath = directoryPath;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load collection node spawns from \"{directory}\".", directoryPath);
            return false;
        }
    }

    public bool TryAddPersistent(string pool, int zoneDefinitionId, Vector4 position, float heading,
        out CollectionNodeSpawnDefinition definition)
    {
        lock (_writeLock)
        {
            definition = new CollectionNodeSpawnDefinition
            {
                Id = Count == 0 ? 1 : Keys.Max() + 1,
                Pool = pool,
                Position = [position.X, position.Y, position.Z],
                Heading = heading,
                ZoneDefinitionId = zoneDefinitionId
            };

            if (zoneDefinitionId <= 0 || !TryAdd(definition.Id, definition))
                return false;

            if (Save(zoneDefinitionId))
                return true;

            Remove(definition.Id);
            return false;
        }
    }

    public bool TryRemovePersistent(int id)
    {
        lock (_writeLock)
        {
            if (!TryGetValue(id, out var definition) || !Remove(id))
                return false;

            if (Save(definition.ZoneDefinitionId))
                return true;

            TryAdd(id, definition);
            return false;
        }
    }

    private bool Save(int zoneDefinitionId)
    {
        if (_directoryPath is null)
            return false;

        var filePath = Path.Combine(_directoryPath, $"{zoneDefinitionId}.json");

        try
        {
            var entries = Values
                .Where(entry => entry.ZoneDefinitionId == zoneDefinitionId)
                .OrderBy(entry => entry.Id);
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });

            var temporaryPath = filePath + ".tmp";
            File.WriteAllText(temporaryPath, json + Environment.NewLine);
            File.Move(temporaryPath, filePath, true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save collection node spawns to \"{file}\".", filePath);
            return false;
        }
    }
}
