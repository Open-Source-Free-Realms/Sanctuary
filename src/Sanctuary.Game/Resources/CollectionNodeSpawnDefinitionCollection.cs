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
            var zoneDirectories = Directory.GetDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly);

            if (zoneDirectories.Length == 0)
            {
                _logger.LogError("No collection node spawn zone directories found in \"{directory}\".", directoryPath);
                return false;
            }

            var loaded = new Dictionary<int, CollectionNodeSpawnDefinition>();
            var loadedPools = new HashSet<(int ZoneDefinitionId, string Pool)>();

            foreach (var zoneDirectory in zoneDirectories.Order())
            {
                var zoneDirectoryName = Path.GetFileName(zoneDirectory);

                if (!int.TryParse(zoneDirectoryName, out var zoneDefinitionId) ||
                    zoneDefinitionId <= 0 || zoneDirectoryName != zoneDefinitionId.ToString())
                {
                    _logger.LogError("Collection node spawn directory \"{directory}\" is not named for a valid zone id.",
                        zoneDirectory);
                    return false;
                }

                foreach (var filePath in Directory.GetFiles(zoneDirectory, "*.json", SearchOption.TopDirectoryOnly).Order())
                {
                    var poolFileName = Path.GetFileNameWithoutExtension(filePath);
                    var pool = poolFileName.Trim().ToLowerInvariant();

                    if (poolFileName != pool || !IsValidPoolKey(pool) || !loadedPools.Add((zoneDefinitionId, pool)))
                    {
                        _logger.LogError("Collection node spawn file \"{file}\" is not named for a valid unique pool.",
                            filePath);
                        return false;
                    }

                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var entries = JsonSerializer.Deserialize<List<CollectionNodeSpawnDefinition>>(stream,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (entries is null || entries.Any(entry => entry.Id <= 0 || entry.Position.Length != 3 ||
                        entry.Position.Any(value => !float.IsFinite(value)) || !float.IsFinite(entry.Heading)))
                    {
                        _logger.LogError("Invalid collection node spawns in \"{file}\".", filePath);
                        return false;
                    }

                    foreach (var entry in entries)
                    {
                        entry.Pool = pool;
                        entry.ZoneDefinitionId = zoneDefinitionId;

                        if (!loaded.TryAdd(entry.Id, entry))
                        {
                            _logger.LogError("Duplicate collection node spawn id {id} in \"{file}\".", entry.Id, filePath);
                            return false;
                        }
                    }
                }
            }

            if (loadedPools.Count == 0)
            {
                _logger.LogError("No collection node spawn files found in \"{directory}\".", directoryPath);
                return false;
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
            pool = pool.Trim().ToLowerInvariant();
            definition = new CollectionNodeSpawnDefinition
            {
                Id = Count == 0 ? 1 : Keys.Max() + 1,
                Pool = pool,
                Position = [position.X, position.Y, position.Z],
                Heading = heading,
                ZoneDefinitionId = zoneDefinitionId
            };

            if (zoneDefinitionId <= 0 || !IsValidPoolKey(pool) || !TryAdd(definition.Id, definition))
                return false;

            if (Save(zoneDefinitionId, pool))
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

            if (Save(definition.ZoneDefinitionId, definition.Pool))
                return true;

            TryAdd(id, definition);
            return false;
        }
    }

    private bool Save(int zoneDefinitionId, string pool)
    {
        if (_directoryPath is null)
            return false;

        var zoneDirectory = Path.Combine(_directoryPath, zoneDefinitionId.ToString());
        var filePath = Path.Combine(zoneDirectory, $"{pool}.json");

        try
        {
            Directory.CreateDirectory(zoneDirectory);
            var entries = Values
                .Where(entry => entry.ZoneDefinitionId == zoneDefinitionId && entry.Pool == pool)
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

    private static bool IsValidPoolKey(string pool)
    {
        return pool.Length > 0 && pool.All(character =>
            char.IsAsciiLetterLower(character) || char.IsDigit(character) || character == '-');
    }
}
