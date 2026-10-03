using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.Resources;

public class ConsumableCollection
{
    private readonly ILogger _logger;

    public ObservableConcurrentDictionary<int, BoomboxDefinition> Boomboxes { get; } = new();
    public ObservableConcurrentDictionary<int, CakeItemDefinition> Cakes { get; } = new();
    public ObservableConcurrentDictionary<int, FoodEffectDefinition> FoodEffects { get; } = new();
    public ObservableConcurrentDictionary<int, TransformAbilityDefinition> Transformations { get; } = new();
    public ObservableConcurrentDictionary<int, RandomTransformFoodDefinition> RandomTransformFoods { get; } = new();
    public ObservableConcurrentDictionary<int, PartyFavorDefinition> PartyFavors { get; } = new();

    public ConsumableCollection(ILogger logger)
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
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter() } // parse CakeItemType from strings ("BossCake"/"ScaredyCake")
            };

            using var jsonDocument = JsonDocument.Parse(fileStream, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip
            });

            foreach (var property in jsonDocument.RootElement.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "boomboxes":
                        ResourceHelper.LoadDefinitions<BoomboxDefinition>(property.Value, jsonSerializerOptions, _logger, filePath,
                            entry => entry.ItemId,
                            entry => entry.ItemId > 0 && entry.EffectIds is not null && entry.DanceSequence is not null
                                && entry.DanceDurationsMs is not null && entry.IndependentDanceDurationsMs is not null
                                && float.IsFinite(entry.Range) && entry.Range > 0 && entry.DurationMs > 0
                                && float.IsFinite(entry.SpawnOffset) && entry.ModelId > 0
                                && entry.DanceBlendMs >= 0 && entry.TransformReapplyDelayMs >= 0
                                && (!(entry.SynchronizedDances || entry.StandingDanceAnimationId == 0) || entry.DanceDurationsMs.Count > 0)
                                && (entry.DanceDurationsMs.Count == 0 || entry.DanceSequence.Length > 0)
                                && entry.DanceDurationsMs.Values.All(durations => durations is not null
                                && durations.Length == entry.DanceSequence.Length && durations.All(duration => duration > 0))
                                && entry.IndependentDanceDurationsMs.Values.All(durations => durations is not null
                                && durations.Count > 0 && durations.All(clip => clip.Key > 0 && clip.Value > 0)),
                            Boomboxes.TryAdd);
                        break;
                    case "cakes":
                        ResourceHelper.LoadDefinitions<CakeItemDefinition>(property.Value, jsonSerializerOptions, _logger, filePath,
                            entry => entry.ItemId,
                            entry => entry.ItemId > 0 && Enum.IsDefined(entry.Type) && entry.ModelId > 0
                                && entry.CooldownMs >= 0 && entry.LifetimeMs > 0 && entry.InteractCooldownMs >= 0
                                && entry.SpawnEffectIds is not null && entry.TransformAbilityIds is not null
                                && entry.ScareGroups is not null && entry.ScareGroups.All(group => group is not null)
                                && entry.OneShotAnimationMs > 0 && entry.OneShotIntervalMs > 0,
                            Cakes.TryAdd);
                        break;
                    case "foodeffects":
                        ResourceHelper.LoadDefinitions<FoodEffectDefinition>(property.Value, jsonSerializerOptions, _logger, filePath,
                            entry => entry.AbilityId, entry => entry.AbilityId > 0 && entry.EffectDelayMs >= 0,
                            FoodEffects.TryAdd);
                        break;
                    case "transformations":
                        ResourceHelper.LoadDefinitions<TransformAbilityDefinition>(property.Value, jsonSerializerOptions, _logger, filePath,
                            entry => entry.AbilityId,
                            entry => entry.AbilityId > 0 && entry.ModelId > 0 && entry.DurationMs > 0 && entry.CooldownMs >= 0,
                            Transformations.TryAdd);
                        break;
                    case "randomtransformfoods":
                        ResourceHelper.LoadDefinitions<RandomTransformFoodDefinition>(property.Value, jsonSerializerOptions, _logger, filePath,
                            entry => entry.ItemId,
                            entry => entry.ItemId > 0 && entry.TransformAbilityIds is not null
                                && entry.TransformAbilityIds.Length > 0 && entry.TransformAbilityIds.All(id => id > 0),
                            RandomTransformFoods.TryAdd);
                        break;
                    case "partyfavors":
                        ResourceHelper.LoadDefinitions<PartyFavorDefinition>(property.Value, jsonSerializerOptions, _logger, filePath,
                            entry => entry.ItemId,
                            entry => entry.ItemId > 0 && entry.CooldownMs >= 0
                                && float.IsFinite(entry.GestureSeconds) && entry.GestureSeconds > 0
                                && float.IsFinite(entry.EffectSeconds) && entry.EffectSeconds > 0
                                && float.IsFinite(entry.Range) && entry.Range > 0,
                            PartyFavors.TryAdd);
                        break;
                }
            }

            _logger.LogInformation("Loaded {count} Boombox definitions.", Boomboxes.Count);
            _logger.LogInformation("Loaded {count} Cake definitions.", Cakes.Count);
            _logger.LogInformation("Loaded {count} FoodEffect definitions.", FoodEffects.Count);
            _logger.LogInformation("Loaded {count} Transformation definitions.", Transformations.Count);
            _logger.LogInformation("Loaded {count} RandomTransformFood definitions.", RandomTransformFoods.Count);
            _logger.LogInformation("Loaded {count} PartyFavor definitions.", PartyFavors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        if (Boomboxes.Count == 0 && FoodEffects.Count == 0 && Transformations.Count == 0 && Cakes.Count == 0
            && RandomTransformFoods.Count == 0 && PartyFavors.Count == 0)
        {
            _logger.LogError("No data was loaded from \"{file}\"", filePath);
            return false;
        }

        return true;
    }
}
