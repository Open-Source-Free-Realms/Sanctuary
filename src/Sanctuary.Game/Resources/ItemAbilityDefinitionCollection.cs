using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources;

public class ItemAbilityDefinitionCollection : ObservableConcurrentDictionary<int, ItemAbilityDefinition>
{
    private readonly ILogger _logger;

    public ItemAbilityDefinitionCollection(ILogger logger)
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
                Converters = { new JsonStringEnumConverter() } // stats are CharacterStatId names ("GoldBoostPercent")
            };

            using var jsonDocument = JsonDocument.Parse(fileStream);

            ResourceHelper.LoadDefinitions<ItemAbilityDefinition>(jsonDocument.RootElement, jsonSerializerOptions, _logger, filePath,
                entry => entry.AbilityId, IsValid, TryAdd);
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

        _logger.LogInformation("Loaded {count} item ability definitions.", Count);

        return true;
    }

    public static bool IsValid(ItemAbilityDefinition entry) =>
        entry.AbilityId > 0 && entry.CooldownMs >= 0 && entry.DurationMs >= 0
        && entry.CastEffectId >= 0 && entry.AuraEffectId >= 0 && entry.BuffIconId >= 0
        && IsValid(entry.Health) && IsValid(entry.Energy)
        // Health and energy over time share one tick (see PlayerEffect).
        && (!IsOverTime(entry.Health) || !IsOverTime(entry.Energy)
            || entry.Health!.OverMs == entry.Energy!.OverMs && entry.Health.IntervalMs == entry.Energy.IntervalMs)
        && float.IsFinite(entry.MovementSpeedBonus)
        && float.IsFinite(entry.Scale) && entry.Scale >= 0
        && entry.Stats is not null && entry.Stats.All(stat => stat is not null
            && stat.Stat != CharacterStatId.InvalidStat && Enum.IsDefined(stat.Stat) && float.IsFinite(stat.Value))
        // Timed parts need a duration; an aura without one is only allowed to last as long as a restore over time.
        && (entry.DurationMs > 0 || entry.MovementSpeedBonus == 0 && entry.Scale == 0 && entry.Stats.Count == 0
            && (entry.AuraEffectId == 0 || IsOverTime(entry.Health) || IsOverTime(entry.Energy)))
        && (entry.CastEffectId != 0 || entry.DurationMs > 0 || entry.Health is not null || entry.Energy is not null);

    public static bool IsOverTime(ItemAbilityRestoreDefinition? restore) => restore is { OverMs: > 0 };

    private static bool IsValid(ItemAbilityRestoreDefinition? restore) =>
        restore is null
        || restore.Amount >= 0 && float.IsFinite(restore.Percent) && restore.Percent >= 0
           && (restore.Amount > 0 || restore.Percent > 0)
           && restore.OverMs >= 0 && restore.IntervalMs > 0 && restore.OverMs % restore.IntervalMs == 0;
}
