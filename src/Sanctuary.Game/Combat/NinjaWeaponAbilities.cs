using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.Combat;

// Ninja shadow-blade weapons grant a common melee slot and one weapon-specific special slot.
// Icon ids are image ids, not image-set ids.
public sealed record WeaponAbility(string Name, int IconImageId, int Damage, int Animation, int EffectId);

public sealed record NinjaWeapon(WeaponAbility Melee, WeaponAbility Special);

public static class NinjaWeaponAbilities
{
    public const int WeaponSlot = 7;
    public const int NinjaProfileId = 2;

    private const int MeleeAnimation = 1099; // com_swing
    private const int MeleeHitFx = 7;        // PFX_Hit_Flash
    // (abil_ninja_shadow_blade's 22599 renders as a leaf in our client; the item sword image is correct.)
    private const int MeleeIcon = 14407;

    // PROVEN-castable AbilityDefinitionIds from the original capture (client renders + lets us cast these).
    private const int MeleeSlotDefId = 4895;
    private const int SpecialSlotDefId = 4899;

    // Live icon-probe override (set by "!ticon <melee> <special>"); null = use the ability's own icon.
    public static int? DebugMeleeIcon;
    public static int? DebugSpecialIcon;

    public static readonly WeaponAbility BareMelee = new("Strike", MeleeIcon, 150, MeleeAnimation, MeleeHitFx);

    // weapon def id -> two abilities. IconImageId is the ability icon image id.
    // Animation ids are confirmed where noted; the remaining specials use the melee swing fallback.
    public static readonly IReadOnlyDictionary<int, NinjaWeapon> ByWeaponDefId = new Dictionary<int, NinjaWeapon>
    {
        [75112] = new(
            new("Twisted Edge",   MeleeIcon, 2870, MeleeAnimation, MeleeHitFx),
            new("Shuriken Storm",     22986, 8302, MeleeAnimation, 15254)), // shuriken hit-flash (lvl5)

        // 75113 - Flame Wave
        [75113] = new(
            new("Cinder Slash",   MeleeIcon, 2609, MeleeAnimation, MeleeHitFx),
            new("Flame Wave",         22974, 10674, MeleeAnimation, 16140)), // PFX_fire_orange_cog_ninja-flame-wave

        // 75110 - Dragonstrike
        [75110] = new(
            new("Flame Flash",    MeleeIcon, 2609, MeleeAnimation, MeleeHitFx),
            new("Dragonstrike",       22965, 10674, 1035, 16186)), // anim com_1hs_special_05 (confirmed); land FX

        // 75111 - 1000 Storms
        [75111] = new(
            new("Lightning Strike", MeleeIcon, 2372, MeleeAnimation, MeleeHitFx),
            new("1000 Storms",          22992, 8302, MeleeAnimation, 16088)), // PFX_lightning_blue_root_ninja-special

        // 75114 - Shadow Armies
        [75114] = new(
            new("Dark Assault",   MeleeIcon, 2608, MeleeAnimation, MeleeHitFx),
            new("Shadow Army",        22989, 3000, MeleeAnimation, 16484)), // PFX_bats_purple_smoke_summon

        // 75115 - Solar Flare (special is "Flaming Uppercut")
        [75115] = new(
            new("Ashen Strike",     MeleeIcon, 2870, MeleeAnimation, MeleeHitFx),
            new("Flaming Uppercut",     22977, 8302, MeleeAnimation, 16119)), // PFX_ninja_flaming-uppercut

        // 75116 - Dragon Breath (special is "Flame Breath")
        [75116] = new(
            new("Fiery Slice",  MeleeIcon, 2609, MeleeAnimation, MeleeHitFx),
            new("Flame Breath",     22971, 10674, MeleeAnimation, 16129)), // PFX_fire_orange_mouth_ninja-flame-breath

        // 75117 - Mysticism (special is "Mystical Blade")
        [75117] = new(
            new("Mystic Rush",    MeleeIcon, 2608, MeleeAnimation, MeleeHitFx),
            new("Mystical Blade",     22980, 3000, MeleeAnimation, 16169)), // WFX_beam-trail_blue-purple_ninja-mystical-blade

        // 75118 - Soul Power (special is "Mystical Drain")
        [75118] = new(
            new("Shadowslash",    MeleeIcon, 2609, MeleeAnimation, MeleeHitFx),
            new("Mystical Drain",     22983, 8302, 1034, 16180)), // anim com_1hs_special_04 (confirmed); AOE-drain FX

        // 75119 - Deception (special is "Fan of Blades")
        [75119] = new(
            new("Hidden Strike", MeleeIcon, 2609, MeleeAnimation, MeleeHitFx),
            new("Fan of Blades",     22968, 5977, MeleeAnimation, 16185)), // PFX_sparkles_multi_cog_ninja-fan-of-blades
    };

    public static readonly int[] AllWeaponDefIds = ByWeaponDefId.Keys.ToArray();

    public static NinjaWeapon? GetEquippedWeapon(Player player)
    {
        var defId = player.GetEquippedWeaponDefinitionId();
        return defId != 0 && ByWeaponDefId.TryGetValue(defId, out var weapon) ? weapon : null;
    }

    // slot 0 = melee, slot 1 = special.
    public static WeaponAbility ResolveAbility(Player player, int slot)
    {
        var weapon = GetEquippedWeapon(player);

        if (weapon is null)
            return BareMelee;

        return slot <= 0 ? weapon.Melee : weapon.Special;
    }

    // Build the 2-slot ability toolbar from the equipped ninja weapon. Slot icon = each ability's real
    // IMAGE_ID (overridable live via !ticon for probing).
    public static AbilityPacketSetDefinition BuildToolbar(Player player, IResourceManager resources)
    {
        var weapon = GetEquippedWeapon(player);

        if (weapon is null)
            return AbilityPacketSetDefinition.CreateEmpty(NinjaProfileId);

        var nameId = 0;
        if (resources.ClientItemDefinitions.TryGetValue(player.GetEquippedWeaponDefinitionId(), out var weaponDef))
            nameId = weaponDef.NameId;

        var def = new AbilityPacketSetDefinition { ProfileId = NinjaProfileId, SlotCount = 8 };

        def.Slots.Add(MakeSlot(MeleeSlotDefId, DebugMeleeIcon ?? weapon.Melee.IconImageId, nameId));
        def.Slots.Add(MakeSlot(SpecialSlotDefId, DebugSpecialIcon ?? weapon.Special.IconImageId, nameId));

        return def;
    }

    private static AbilityPacketSetDefinition.Slot MakeSlot(int abilityDefId, int iconId, int nameId) => new()
    {
        Type = 3,
        Unknown2 = abilityDefId,
        ManaCost = 0,
        IconId = iconId,
        NameId = nameId,
        AbilityDefinitionId = abilityDefId,
    };
}
