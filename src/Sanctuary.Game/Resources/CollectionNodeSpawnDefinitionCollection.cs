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
    private string? _filePath;

    public CollectionNodeSpawnDefinitionCollection(ILogger logger)
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
            var entries = JsonSerializer.Deserialize<List<CollectionNodeSpawnDefinition>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (entries is null || entries.Any(entry => entry.Id <= 0 || string.IsNullOrWhiteSpace(entry.Pool) ||
                entry.Position.Length != 3) ||
                entries.Select(entry => entry.Id).Distinct().Count() != entries.Count)
            {
                _logger.LogError("Invalid collection node spawns in \"{file}\".", filePath);
                return false;
            }

            var loaded = entries.ToDictionary(entry => entry.Id);

            foreach (var entry in loaded.Values)
                entry.Pool = entry.Pool.Trim().ToLowerInvariant();

            lock (_writeLock)
            {
                foreach (var entry in loaded)
                    this[entry.Key] = entry.Value;

                foreach (var id in Keys.Where(id => !loaded.ContainsKey(id)).ToArray())
                    Remove(id);

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

    public bool TryAddPersistent(string pool, Vector4 position, float heading, out CollectionNodeSpawnDefinition definition)
    {
        lock (_writeLock)
        {
            definition = new CollectionNodeSpawnDefinition
            {
                Id = Count == 0 ? 1 : Keys.Max() + 1,
                Pool = pool,
                Position = [position.X, position.Y, position.Z],
                Heading = heading
            };

            if (!TryAdd(definition.Id, definition))
                return false;

            if (Save())
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

            if (Save())
                return true;

            TryAdd(id, definition);
            return false;
        }
    }

    private bool Save()
    {
        if (_filePath is null)
            return false;

        try
        {
            var json = JsonSerializer.Serialize(Values.OrderBy(entry => entry.Id), new JsonSerializerOptions
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
            _logger.LogError(ex, "Failed to save collection node spawns to \"{file}\".", _filePath);
            return false;
        }
    }
}
