using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions.Combat;

namespace Sanctuary.Game.Resources;

public sealed class AbilityDefinitionCollection : ObservableConcurrentDictionary<int, AbilityDefinition>
{
    public static readonly IReadOnlySet<string> EffectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SweepDamage",
        "AoeDamage",
        "SingleTargetDamage",
        "AoeDamageHeal",
        "Summon",
        "Buff",
    };

    private readonly ILogger _logger;
    private readonly object _writeLock = new();

    public AbilityDefinitionCollection(ILogger logger)
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
            var entries = JsonSerializer.Deserialize<List<AbilityDefinition>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (entries is null || entries.Count == 0)
            {
                _logger.LogError("No ability definitions found in \"{file}\".", filePath);
                return false;
            }

            var loaded = new Dictionary<int, AbilityDefinition>();

            foreach (var entry in entries)
            {
                if (entry.Id <= 0)
                {
                    _logger.LogError("Invalid ability definition {id} in \"{file}\".", entry.Id, filePath);
                    return false;
                }

                if (!EffectTypes.Contains(entry.EffectType))
                {
                    _logger.LogWarning("Skipping ability definition {id} with unimplemented effect type \"{effectType}\" in \"{file}\".", entry.Id, entry.EffectType, filePath);
                    continue;
                }

                if (!loaded.TryAdd(entry.Id, entry))
                {
                    _logger.LogError("Duplicate ability definition {id} in \"{file}\".", entry.Id, filePath);
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
