using System.Collections.Generic;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// What an item's ActivatableAbilityId does when used: potions, scrolls and buff foods.
/// Generated from client data by ClientMods/extract_item_abilities.py.
/// </summary>
public class ItemAbilityDefinition
{
    public int AbilityId { get; set; }

    /// <summary>
    /// Buffs in the same group replace each other instead of stacking (e.g. two coin scrolls).
    /// </summary>
    public string? Group { get; set; }

    public int CooldownMs { get; set; }

    /// <summary>
    /// How long the buff (stats, speed, scale, aura) lasts. 0 for instant abilities.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Composite effect played once on use.
    /// </summary>
    public int CastEffectId { get; set; }

    /// <summary>
    /// Composite effect shown on the player for as long as the buff lasts.
    /// </summary>
    public int AuraEffectId { get; set; }

    /// <summary>
    /// Buff bar icon. 0 uses the item's own icon.
    /// </summary>
    public int BuffIconId { get; set; }

    public ItemAbilityRestoreDefinition? Health { get; set; }
    public ItemAbilityRestoreDefinition? Energy { get; set; }

    public float MovementSpeedBonus { get; set; }
    public float Scale { get; set; }

    public List<ItemAbilityStatDefinition> Stats { get; set; } = [];
}

public class ItemAbilityRestoreDefinition
{
    public int Amount { get; set; }

    /// <summary>
    /// Percent of the maximum, added to <see cref="Amount"/>.
    /// </summary>
    public float Percent { get; set; }

    /// <summary>
    /// Spreads the restore over this long, one tick per <see cref="IntervalMs"/>. 0 restores it all at once.
    /// </summary>
    public int OverMs { get; set; }

    public int IntervalMs { get; set; } = 1000;
}

public class ItemAbilityStatDefinition
{
    public CharacterStatId Stat { get; set; }
    public float Value { get; set; }
}
