using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Applies an <see cref="ItemAbilityDefinition"/> to a player: the instant part of a heal/energy restore,
/// the cast effect, and one tracked <see cref="PlayerEffect"/> for whatever lasts (stats, speed, scale,
/// aura, restore over time).
/// </summary>
public static class ItemAbilityHelper
{
    // Restores over time run a little past their last tick, so the expiry never beats it.
    private const int OverTimeGraceMs = 500;

    public static void Apply(Player player, ItemAbilityDefinition ability, int nameId, int iconId)
    {
        // The appearance change plays the cast effect as its poof and brings its own change-form buff icon.
        if (ability.ModelId != 0)
            player.ApplyTemporaryAppearance(ability.ModelId, ability.DurationMs, ability.CastEffectId, nameId);
        else if (ability.CastEffectId != 0)
        {
            player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
            {
                Guid = player.Guid,
                CompositeEffectId = ability.CastEffectId,
                Clear = true
            }, true);
        }

        if (!string.IsNullOrEmpty(ability.Group))
            player.RemoveEffectGroup(ability.Group);

        // Timed stats go first, so a MaxHealth/MaxMana bonus raises the cap before the restore fills it.
        var lasting = CreateEffect(player, ability, nameId, iconId);

        if (lasting is not null)
            player.AddEffect(lasting);

        if (ability.Health is { OverMs: 0 } health)
            player.Heal(GetAmount(health, player.MaxHitpoints));

        if (ability.Energy is { OverMs: 0 } energy)
            player.RestoreEnergy(GetAmount(energy, player.EffectiveMaxEnergy));
    }

    public static int GetAmount(ItemAbilityRestoreDefinition restore, int maximum) =>
        restore.Amount + (int)MathF.Round(maximum * restore.Percent / 100f);

    /// <summary>
    /// The lasting part of an ability (stats, speed, scale, aura, restore over time), or null if it's instant.
    /// </summary>
    public static PlayerEffect? CreateEffect(Player player, ItemAbilityDefinition ability, int nameId, int iconId)
    {
        var health = ItemAbilityDefinitionCollection.IsOverTime(ability.Health) ? ability.Health : null;
        var energy = ItemAbilityDefinitionCollection.IsOverTime(ability.Energy) ? ability.Energy : null;

        // Validation makes health and energy over time share one tick, so either one sets it.
        var overTime = health ?? energy;

        var timed = ability.AuraEffectId != 0 || ability.MovementSpeedBonus != 0 || ability.Scale != 0 || ability.Stats.Count > 0;

        if (!timed && overTime is null)
            return null;

        var intervalMs = overTime?.IntervalMs ?? 0;
        var tickCount = overTime is null ? 0 : overTime.OverMs / intervalMs;

        var durationMs = ability.DurationMs > 0 ? ability.DurationMs : overTime!.OverMs + OverTimeGraceMs;

        return new PlayerEffect
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds(durationMs),
            Group = ability.Group,
            WorldEffectId = ability.AuraEffectId,
            // Ungrouped abilities (fireworks) only play an effect for a few seconds; they aren't buffs.
            BuffIconId = string.IsNullOrEmpty(ability.Group) ? 0 : ability.BuffIconId != 0 ? ability.BuffIconId : iconId,
            BuffNameId = nameId,
            MovementSpeedBonus = ability.MovementSpeedBonus,
            Scale = ability.Scale,
            StatBonuses = ability.Stats.Count > 0
                ? ability.Stats.GroupBy(stat => stat.Stat).ToDictionary(group => group.Key, group => group.Sum(stat => stat.Value))
                : null,
            TickIntervalMs = intervalMs,
            TickCount = tickCount,
            HealthPerTick = health is not null ? GetAmount(health, player.MaxHitpoints) / tickCount : 0,
            EnergyPerTick = energy is not null ? GetAmount(energy, player.EffectiveMaxEnergy) / tickCount : 0
        };
    }
}
