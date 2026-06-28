using System;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

/// <summary>
/// Loads and manages consumable items from the consolidated Consumables.json file.
/// Provides access to Boomboxes, FoodEffects, and Transformations.
/// </summary>
public class ConsumableCollection
{
    private readonly ILogger _logger;

    public BoomboxDefinitionCollection Boomboxes { get; }
    public FoodEffectCollection FoodEffects { get; }
    public TransformAbilityCollection Transformations { get; }

    public ConsumableCollection(ILogger logger)
    {
        _logger = logger;
        Boomboxes = new BoomboxDefinitionCollection(logger);
        FoodEffects = new FoodEffectCollection(logger);
        Transformations = new TransformAbilityCollection(logger);
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

            var consumables = JsonSerializer.Deserialize<ConsumableDefinitions>(fileStream, jsonSerializerOptions);

            if (consumables is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            // Load Boomboxes
            foreach (var entry in consumables.Boomboxes)
            {
                if (!Boomboxes.TryAdd(entry.ItemId, entry))
                {
                    _logger.LogWarning("Failed to add Boombox entry. ItemId={id} \"{file}\"", entry.ItemId, filePath);
                    return false;
                }
            }
            _logger.LogInformation("Loaded {count} Boombox definitions.", Boomboxes.Count);

            // Load FoodEffects
            foreach (var entry in consumables.FoodEffects)
            {
                if (!FoodEffects.TryAdd(entry.AbilityId, entry))
                {
                    _logger.LogWarning("Failed to add FoodEffect entry. AbilityId={id} \"{file}\"", entry.AbilityId, filePath);
                    return false;
                }
            }
            _logger.LogInformation("Loaded {count} FoodEffect definitions.", FoodEffects.Count);

            // Load Transformations
            foreach (var entry in consumables.Transformations)
            {
                if (!Transformations.TryAdd(entry.AbilityId, entry))
                {
                    _logger.LogWarning("Failed to add Transformation entry. AbilityId={id} \"{file}\"", entry.AbilityId, filePath);
                    return false;
                }
            }
            _logger.LogInformation("Loaded {count} Transformation definitions.", Transformations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        if (Boomboxes.Count == 0 && FoodEffects.Count == 0 && Transformations.Count == 0)
        {
            _logger.LogError("No data was loaded from \"{file}\"", filePath);
            return false;
        }

        return true;
    }
}
