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
        if (ability.CastEffectId != 0)
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

    private static PlayerEffect? CreateEffect(Player player, ItemAbilityDefinition ability, int nameId, int iconId)
    {
        var health = ItemAbilityDefinitionCollection.IsOverTime(ability.Health) ? ability.Health : null;
        var energy = ItemAbilityDefinitionCollection.IsOverTime(ability.Energy) ? ability.Energy : null;

        if (ability.DurationMs == 0 && health is null && energy is null)
            return null;

        var intervalMs = health?.IntervalMs ?? energy!.IntervalMs;
        var overTimeMs = Math.Max(health?.OverMs ?? 0, energy?.OverMs ?? 0);
        var tickCount = overTimeMs / intervalMs;

        var durationMs = ability.DurationMs > 0 ? ability.DurationMs : overTimeMs + OverTimeGraceMs;

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
            TickIntervalMs = health is not null || energy is not null ? intervalMs : 0,
            TickCount = tickCount,
            HealthPerTick = health is not null ? GetAmount(health, player.MaxHitpoints) / Math.Max(1, health.OverMs / intervalMs) : 0,
            EnergyPerTick = energy is not null ? GetAmount(energy, player.EffectiveMaxEnergy) / Math.Max(1, energy.OverMs / intervalMs) : 0
        };
    }
}
