using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.Resources;

public class PetDefinitionCollection : ObservableConcurrentDictionary<int, PetDefinition>
{
    private readonly ILogger _logger;

    public PetDefinitionCollection(ILogger logger)
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
                IncludeFields = true
            };

            using var jsonDocument = JsonDocument.Parse(fileStream);

            ResourceHelper.LoadDefinitions<PetDefinition>(jsonDocument.RootElement, jsonSerializerOptions, _logger, filePath,
                entry => entry.Id,
                entry => entry.Id > 0 && entry.PetTricks is not null && entry.ItemClasses is not null
                    && entry.PetTricks.All(trick => trick is not null && trick.Id > 0)
                    && entry.PetTricks.Select(trick => trick.Id).Distinct().Count() == entry.PetTricks.Count
                    && entry.ItemClasses.All(itemClass => itemClass.Value is not null)
                    && entry.ItemClasses.Select(itemClass => itemClass.Key).Distinct().Count() == entry.ItemClasses.Count
                    && entry.TrickAnimations is not null && entry.TrickAnimations.All(trick => trick.Key > 0
                        && entry.PetTricks.Any(petTrickInfo => petTrickInfo.Id == trick.Key)
                        && trick.Value is not null && trick.Value.AnimationId > 0 && trick.Value.OwnerAnimationId >= 0
                        && trick.Value.DurationMs > 0
                        && trick.Value.StartDelayMs >= 0)
                    && entry.IdleAnimations is not null && entry.IdleAnimations.All(IsValidIdleAnimation)
                    && entry.BathroomAnimations is not null && entry.BathroomAnimations.All(IsValidIdleAnimation)
                    && entry.RestPostures is not null && entry.RestPostures.All(restPosture => restPosture is not null
                        && restPosture.EnterAnimationId > 0 && restPosture.EnterDurationMs > 0
                        && restPosture.ExitAnimationId > 0 && restPosture.ExitDurationMs > 0
                        && restPosture.LoopAnimationIds is not null && restPosture.LoopAnimationIds.Count > 0
                        && restPosture.LoopAnimationIds.All(loopAnimationId => loopAnimationId > 0)
                        && restPosture.FidgetAnimations is not null && restPosture.FidgetAnimations.All(IsValidIdleAnimation)),
                TryAdd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        if (Count == 0)
        {
            _logger.LogError("No data was loaded. \"{file}\"", filePath);
            return false;
        }

        return true;
    }

    private static bool IsValidIdleAnimation(PetIdleAnimationDefinition? idleAnimation) =>
        idleAnimation is not null && idleAnimation.AnimationId > 0 && idleAnimation.DurationMs > 0;
}
