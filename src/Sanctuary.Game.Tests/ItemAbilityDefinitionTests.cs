using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ItemAbilityDefinitionTests
{
    private static readonly string Resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));

    [TestMethod]
    public void ItemAbilitiesJson_LoadsEveryEntry()
    {
        var path = Path.Combine(Resources, "ItemAbilities.json");
        var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(abilities.Load(path));

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.AreEqual(document.RootElement.GetArrayLength(), abilities.Count, "Every entry should be valid and unique.");

        Assert.IsTrue(abilities.TryGetValue(1207, out var healthPotion), "Health Potion");
        Assert.AreEqual(50f, healthPotion.Health!.Percent);

        Assert.IsTrue(abilities.TryGetValue(244, out var coinScroll), "Bonus Coin Scroll");
        Assert.IsTrue(coinScroll.DurationMs > 0);
        Assert.AreEqual(CharacterStatId.GoldBoostPercent, coinScroll.Stats.Single().Stat);

        Assert.IsTrue(abilities.TryGetValue(1999, out var penguinFood), "Illusion Food - Penguin");
        Assert.AreEqual(16, penguinFood.ModelId);
    }

    [TestMethod]
    public void CreateEffect_BuildsEveryShippedAbility()
    {
        var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(abilities.Load(Path.Combine(Resources, "ItemAbilities.json")));

        var player = new Player(null!, null!, null!, null!);

        foreach (var ability in abilities.Values)
        {
            var effect = ItemAbilityHelper.CreateEffect(player, ability, nameId: 1, iconId: 1);
            var overTime = ItemAbilityDefinitionCollection.IsOverTime(ability.Health) || ItemAbilityDefinitionCollection.IsOverTime(ability.Energy);
            var timed = ability.AuraEffectId != 0 || ability.MovementSpeedBonus != 0 || ability.Scale != 0 || ability.Stats.Count > 0;

            Assert.AreEqual(timed || overTime, effect is not null, $"Ability {ability.AbilityId}");

            if (effect is null)
                continue;

            Assert.IsTrue(effect.ExpiresAt > DateTimeOffset.UtcNow, $"Ability {ability.AbilityId}");
            Assert.AreEqual(overTime, effect.TickCount > 0, $"Ability {ability.AbilityId}");
        }
    }

    [TestMethod]
    public void CreateEffect_SplitsRestoreOverTimeIntoTicks()
    {
        var player = new Player(null!, null!, null!, null!);
        var ability = new ItemAbilityDefinition
        {
            AbilityId = 1,
            AuraEffectId = 10,
            Health = new ItemAbilityRestoreDefinition { Percent = 25, OverMs = 5000 }
        };

        var effect = ItemAbilityHelper.CreateEffect(player, ability, 1, 1)!;

        Assert.AreEqual(5, effect.TickCount);
        Assert.AreEqual(1000, effect.TickIntervalMs);
        Assert.AreEqual(player.MaxHitpoints / 4 / 5, effect.HealthPerTick);
        Assert.AreEqual(0, effect.EnergyPerTick);
    }

    [TestMethod]
    public void CreateEffect_TimedBuffWithoutRestoreHasNoTicks()
    {
        var player = new Player(null!, null!, null!, null!);
        var ability = new ItemAbilityDefinition
        {
            AbilityId = 244,
            Group = "CoinBoost",
            DurationMs = 60000,
            Stats = [new ItemAbilityStatDefinition { Stat = CharacterStatId.GoldBoostPercent, Value = 25 }]
        };

        var effect = ItemAbilityHelper.CreateEffect(player, ability, 1, 1)!;

        Assert.AreEqual(0, effect.TickCount);
        Assert.AreEqual(25f, effect.StatBonuses![CharacterStatId.GoldBoostPercent]);
    }

    [TestMethod]
    public void ItemAbilitiesJson_ModelsExist()
    {
        var models = new ModelDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(models.Load(Path.Combine(Resources, "Models.txt")));

        var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(abilities.Load(Path.Combine(Resources, "ItemAbilities.json")));

        CollectionAssert.AreEqual(Array.Empty<int>(),
            abilities.Values.Where(ability => ability.ModelId != 0 && !models.ContainsKey(ability.ModelId)).Select(ability => ability.AbilityId).ToArray());
    }

    [TestMethod]
    public void ItemAbilitiesJson_DoesNotRedefineConsumables()
    {
        // Food auras and transformations are matched first, so a duplicate here would be dead data.
        using var consumables = JsonDocument.Parse(File.ReadAllText(Path.Combine(Resources, "Consumables.json")));
        var claimed = new[] { "FoodEffects", "Transformations" }
            .SelectMany(key => consumables.RootElement.GetProperty(key).EnumerateArray())
            .Select(entry => entry.GetProperty("AbilityId").GetInt32())
            .ToHashSet();

        var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(abilities.Load(Path.Combine(Resources, "ItemAbilities.json")));

        CollectionAssert.AreEqual(Array.Empty<int>(), abilities.Keys.Where(claimed.Contains).ToArray());
    }

    [TestMethod]
    public void Load_RejectsTimedStatsWithoutDuration()
    {
        var path = Path.Combine(Path.GetTempPath(), $"item-abilities-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "[{\"AbilityId\":1,\"Stats\":[{\"Stat\":\"GoldBoostPercent\",\"Value\":25}]}," +
            "{\"AbilityId\":2,\"Health\":{\"Percent\":25,\"OverMs\":5000},\"Energy\":{\"Percent\":25,\"OverMs\":3000}}," +
            "{\"AbilityId\":3,\"DurationMs\":60000,\"Stats\":[{\"Stat\":\"GoldBoostPercent\",\"Value\":25}]}]");

        try
        {
            var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);

            Assert.IsTrue(abilities.Load(path));
            CollectionAssert.AreEqual(new[] { 3 }, abilities.Keys.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ApplyPercentBoost_AddsPercentAndRounds()
    {
        Assert.AreEqual(125, Player.ApplyPercentBoost(100, 25));
        Assert.AreEqual(15, Player.ApplyPercentBoost(10, 50));
        Assert.AreEqual(3, Player.ApplyPercentBoost(2, 25), "Half a coin rounds in the player's favor.");
        Assert.AreEqual(100, Player.ApplyPercentBoost(100, 0));
        Assert.AreEqual(0, Player.ApplyPercentBoost(0, 100));
        Assert.AreEqual(int.MaxValue, Player.ApplyPercentBoost(int.MaxValue, 100));
    }

    [TestMethod]
    public void GetAmount_AddsFlatAmountToPercentOfMaximum()
    {
        Assert.AreEqual(1250, ItemAbilityHelper.GetAmount(new ItemAbilityRestoreDefinition { Percent = 50 }, 2500));
        Assert.AreEqual(1350, ItemAbilityHelper.GetAmount(new ItemAbilityRestoreDefinition { Amount = 100, Percent = 50 }, 2500));
        Assert.AreEqual(100, ItemAbilityHelper.GetAmount(new ItemAbilityRestoreDefinition { Amount = 100 }, 2500));
    }
}
