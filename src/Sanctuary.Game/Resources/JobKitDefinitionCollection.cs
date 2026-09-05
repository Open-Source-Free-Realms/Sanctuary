using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions.Combat;

namespace Sanctuary.Game.Resources;

public sealed class JobKitDefinitionCollection : ObservableConcurrentDictionary<int, JobKitDefinition>
{
    private readonly ILogger _logger;
    private readonly object _writeLock = new();

    public JobKitDefinitionCollection(ILogger logger)
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
            var entries = JsonSerializer.Deserialize<List<JobKitDefinition>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (entries is null || entries.Count == 0)
            {
                _logger.LogError("No job kits found in \"{file}\".", filePath);
                return false;
            }

            var loaded = new Dictionary<int, JobKitDefinition>();

            foreach (var entry in entries)
            {
                if (entry.ProfileId <= 0 || entry.Weapons.Any(w => w.WeaponDefIds.Count == 0))
                {
                    _logger.LogError("Invalid job kit {id} in \"{file}\".", entry.ProfileId, filePath);
                    return false;
                }

                if (!loaded.TryAdd(entry.ProfileId, entry))
                {
                    _logger.LogError("Duplicate job kit {id} in \"{file}\".", entry.ProfileId, filePath);
                    return false;
                }
            }

            lock (_writeLock)
            {
                foreach (var entry in loaded)
                    this[entry.Key] = entry.Value;

                foreach (var key in Keys.Where(key => !loaded.ContainsKey(key)).ToArray())
                    Remove(key);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }
    }
}
