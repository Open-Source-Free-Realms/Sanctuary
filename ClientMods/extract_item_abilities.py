#!/usr/bin/env python3
"""Builds src/Resources/ItemAbilities.json, the server-side behavior of potions, scrolls and buff foods.

Items only carry an ActivatableAbilityId; what that ability does lived on the original servers. This
script rebuilds it from what the client still ships:

  - Locale/en_us_data.*                   item names and descriptions ("restores some of your health
                                          over 5 seconds", "+% Coins", ...), which pick the behavior
  - Resources/AbilityEffectTypeSettings   the buff tooltips and the CharacterStatId each one reads
                                          (+% Coins -> GoldBoostPercent, Talent -> M3SpecialSpawnRate, ...)
  - Resources/ActorCompositeEffectDefinitions.xml
                                          every composite effect id used below is checked against it,
                                          and looping effects are played as timed auras, not one-shots
  - src/Resources/Models.txt              illusion food models are checked against it

Abilities already handled through Consumables.json (food auras, transformations) are skipped.

Example:
  python extract_item_abilities.py --client "%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client"
"""

import json
import re
import xml.etree.ElementTree as ET
from argparse import ArgumentParser
from collections import defaultdict
from pathlib import Path

from extract_names import load_locale, lookup2

script_dir = Path(__file__).resolve().parent
resources_dir = script_dir.parent / "src" / "Resources"

SECOND = 1000
MINUTE = 60 * SECOND

POTION_COOLDOWN_MS = 30 * SECOND
REFILL_COOLDOWN_MS = 2 * MINUTE   # reusable potions that "slowly refill" after each use
BUFF_COOLDOWN_MS = 5 * SECOND
FIREWORK_COOLDOWN_MS = 10 * SECOND
FOOD_DURATION_MS = 30 * MINUTE
LOOP_CAST_DURATION_MS = 10 * SECOND

HEAL_EFFECT = 16324        # PFX_heal_health_red_sm_short_head
ENERGY_EFFECT = 16325      # PFX_heal_mana_blue_sm_short_head
REPLENISH_EFFECT = 16326   # PFX_heal_health-mana_purple_sm_short_head
HEAL_AURA = 15858          # PFX_magic-heal_red_head_shower_lg_loop
COIN_EFFECT = 5245         # PFX_coins_gold_root_money-reward
STAR_EFFECT = 15534        # PFX_stars_yellow_root_exp-reward
POWERUP_EFFECT = 40        # PFX_magic-starburst_blue_chest_powerup
SPEED_AURA = 15216         # PFX_beam_white_feet_speed-run_loop (the pet speed buff uses it too)
LIGHTNING_SPEED_AURA = 15049  # PFX_beam_red_feet_speed-run_loop
SHIELD_EFFECT = 15855      # PFX_shield_green_head_defense-buff
TRANSFIGURE_EFFECT = 5332  # PFX_swirl-flash_purple_root_transfiguration-land
INVISIBILITY_AURA = 15570  # PFX_smoke_dark_root_invisibility-indicator_loop
TRANSFORM_POOF = 21


def restore(percent, over_ms=0):
    return {"Percent": percent, "OverMs": over_ms} if over_ms else {"Percent": percent}


def heal(percent, over_ms=0, cooldown=POTION_COOLDOWN_MS):
    ability = {"Group": "Health", "CooldownMs": cooldown, "CastEffectId": HEAL_EFFECT, "Health": restore(percent, over_ms)}
    if over_ms:
        ability["AuraEffectId"] = HEAL_AURA
    return ability


def energy(percent, over_ms=0):
    return {"Group": "Energy", "CooldownMs": POTION_COOLDOWN_MS, "CastEffectId": ENERGY_EFFECT, "Energy": restore(percent, over_ms)}


def replenish(percent, over_ms=0):
    return {"Group": "Health", "CooldownMs": POTION_COOLDOWN_MS, "CastEffectId": REPLENISH_EFFECT,
            "Health": restore(percent, over_ms), "Energy": restore(percent, over_ms)}


def stats(group, duration_ms, cast_effect, **values):
    return {"Group": group, "CooldownMs": BUFF_COOLDOWN_MS, "DurationMs": duration_ms, "CastEffectId": cast_effect,
            "Stats": [{"Stat": stat, "Value": value} for stat, value in values.items()]}


def coins(percent, duration_ms):
    return stats("CoinBoost", duration_ms, COIN_EFFECT, GoldBoostPercent=percent)


def stars(percent, duration_ms):
    return stats("StarBoost", duration_ms, STAR_EFFECT, ExperienceBoostPercent=percent)


def speed(bonus, duration_ms, aura=SPEED_AURA):
    return {"Group": "Speed", "CooldownMs": BUFF_COOLDOWN_MS, "DurationMs": duration_ms, "AuraEffectId": aura,
            "MovementSpeedBonus": bonus}


def scale(value, duration_ms=FOOD_DURATION_MS):
    return {"Group": "Scale", "CooldownMs": BUFF_COOLDOWN_MS, "DurationMs": duration_ms, "CastEffectId": TRANSFIGURE_EFFECT,
            "Scale": value}


def aura(effect_id, duration_ms=FOOD_DURATION_MS, group="Aura"):
    return {"Group": group, "CooldownMs": BUFF_COOLDOWN_MS, "DurationMs": duration_ms, "AuraEffectId": effect_id}


def cast(effect_id, cooldown=FIREWORK_COOLDOWN_MS):
    return {"CooldownMs": cooldown, "CastEffectId": effect_id}


def transform(model_id, duration_ms=FOOD_DURATION_MS):
    # Same poof (21) and 30 minute default as the Consumables.json transformations.
    return {"Group": "Transform", "CooldownMs": BUFF_COOLDOWN_MS, "DurationMs": duration_ms, "CastEffectId": TRANSFORM_POOF,
            "ModelId": model_id}


# Tiers follow the item names (I/1, II, III, IV, V) and descriptions ("some", "a large portion").
ABILITIES = {
    # Health potions
    1207: heal(50),                         # Health Potion: "Health FTW!"
    1636: heal(25), 1637: heal(35), 2070: heal(65),   # Health Potion 1 / II / IV
    267: heal(25, 5 * SECOND),              # Small Health Potion: "over 5 seconds"
    4697: heal(50, 5 * SECOND),             # Medium / Gigantic Health Potion
    4698: heal(75, 5 * SECOND),             # Large / Ginormous Health Potion
    230: heal(75), 1924: heal(75),          # Jumbo / Deprecated Health Potion: "a large portion"
    3093: heal(40, 15 * SECOND), 1598: heal(50, 15 * SECOND), 1599: heal(55, 15 * SECOND),
    1600: heal(65, 15 * SECOND), 1601: heal(60, 15 * SECOND),   # Heal Over Time potions
    4696: heal(30, cooldown=BUFF_COOLDOWN_MS), 1638: heal(30, cooldown=BUFF_COOLDOWN_MS),   # Mushroom Pizza Slice
    235: heal(20, cooldown=BUFF_COOLDOWN_MS),                   # Whole Milk Carton

    # Energy potions
    1208: energy(50),                       # Mana Potion
    1639: energy(25), 1640: energy(35), 2072: energy(65),       # Energy Potion 1 / Mana II / IV
    4694: energy(50, 5 * SECOND),           # Medium / Wheeler Energy Potion
    4695: energy(75, 5 * SECOND),           # Large Energy Potion
    229: energy(75),                        # Jumbo Energy Potion

    # Replenishment (health and energy)
    4699: replenish(25), 4700: replenish(50), 4701: replenish(75),   # Small / Medium / Large: "instantly"
    3109: replenish(25), 3110: replenish(35), 3112: replenish(65),   # Replenishing Potion 1 / II / IV
    231: replenish(75),                     # Jumbo Replenishing Potion

    # Coin and star scrolls (GoldBoostPercent / ExperienceBoostPercent, "+% Coins" / "+% Stars")
    244: coins(25, 15 * MINUTE), 243: stars(25, 15 * MINUTE),   # Bonus Coin / Star Scroll: "a short amount of time"
    206: coins(50, 30 * MINUTE), 203: stars(50, 30 * MINUTE),   # Super Coin / Star Booster, AAA Star Booster
    239: coins(25, 30 * MINUTE), 240: stars(25, 30 * MINUTE),   # Wheeler Coin / Star Booster Scroll
    223: coins(100, 15 * MINUTE),           # Crazy Coin Scroll: "Feeling lucky?"
    222: stars(100, 15 * MINUTE),           # Super Star Scroll
    251: coins(50, 60 * MINUTE),            # Prospector's Scroll: "for an hour"

    # Run speed (MovementSpeedBonus on top of the 8 m/s on-foot speed)
    250: {**speed(4, 60 * SECOND, LIGHTNING_SPEED_AURA), "CooldownMs": REFILL_COOLDOWN_MS},   # Lightning Elixir: "for 60 seconds"
    1955: speed(1, FOOD_DURATION_MS), 1954: speed(1.5, FOOD_DURATION_MS), 1953: speed(2, FOOD_DURATION_MS),
    1952: speed(2.5, FOOD_DURATION_MS), 1951: speed(3, FOOD_DURATION_MS),   # Speed Buff Food I-V
    1931: speed(2, FOOD_DURATION_MS),       # Pixachino: "makes you move faster"
    1581: speed(-3.2, 30 * SECOND, 0),      # (QA) You be stuck!: "-40% for 30 seconds"

    # Size
    1950: scale(1.15), 1949: scale(1.3), 1948: scale(1.5), 1947: scale(1.75), 1946: scale(2),   # Grow Food I-V
    1945: scale(0.85), 1944: scale(0.75), 1943: scale(0.65), 1942: scale(0.55),   # Shrink Food I-IV
    1941: scale(0.4),                       # Tiny Tincture
    1915: scale(0.5),                       # Cupcake of Tim the Tiny: "makes the world get bigger"
    1916: scale(2),                         # Thousand Pound Cake: "makes the world shrink"
    3264: {**scale(1.3), "Stats": [{"Stat": "MaxHealth", "Value": 500}]},   # Strong Arm Stew: health and size

    # Combat buffs
    264: {**stats("Defense", 30 * SECOND, SHIELD_EFFECT, DamageReductionPercent=25),
          "CooldownMs": REFILL_COOLDOWN_MS},   # Darkthorne's Embrace: "reduces the damage you take for 30 seconds"

    # Invisibility foods. There's no hide packet here yet, so they only show the indicator and the buff.
    1940: aura(INVISIBILITY_AURA, 1 * MINUTE, "Invisibility"), 1939: aura(INVISIBILITY_AURA, 2 * MINUTE, "Invisibility"),
    1938: aura(INVISIBILITY_AURA, 3 * MINUTE, "Invisibility"), 1937: aura(INVISIBILITY_AURA, 4 * MINUTE, "Invisibility"),
    1936: aura(INVISIBILITY_AURA, 5 * MINUTE, "Invisibility"),

    # Cosmetic potions and treats
    3855: aura(15208), 3856: aura(15222), 3857: aura(15291), 3858: aura(15292),   # Button Eyes Potions: red, orange
    3859: aura(15294), 3860: aura(15296), 3861: aura(15297), 3862: aura(15298),   # yellow, green
    3863: aura(15299), 3864: aura(15300), 3866: aura(15303), 3867: aura(15301),   # cyan, blue
    3868: aura(15304), 3869: aura(15305), 3870: aura(15306), 3871: aura(15307),   # purple, magenta
    3873: aura(15363), 3874: aura(15362), 3875: aura(15369),   # Firefly Swirls, Butterfly Swirls, Flower Trail
    3804: aura(15589),                      # Glowing Contacts
    224: aura(15531),                       # Crimson Rose: "It feels like spring time."
    1934: aura(15459),                      # Heart Candy
    1920: aura(15653),                      # Rainbowopsicle
    1935: aura(15477),                      # Glow Cookie: "good fortune radiates from your fingers"
    3794: aura(5568, 30 * SECOND),          # Superstar Spotlight
    5157: aura(16460, 30 * SECOND), 5158: aura(16487, 30 * SECOND),   # Red / Black Petal
    3791: aura(16129, LOOP_CAST_DURATION_MS),   # Flaming Jalepeno Pepper
    3792: aura(630, LOOP_CAST_DURATION_MS),     # Icicle Ice Cream
    3796: aura(2, LOOP_CAST_DURATION_MS),       # Seeing Stars Spyglass
    1932: cast(5376, BUFF_COOLDOWN_MS),     # Chugawug Brew / Sweetwater Sasparilla: burp
    1933: cast(15608, BUFF_COOLDOWN_MS),    # Laughter Latte
    3790: cast(5456),                       # Bottle of Butterflies

    # Illusion foods and masks Consumables.json doesn't cover. Models are the race-assigned rows of
    # Models.txt (the ones the existing transformations use), not the later duplicates.
    1975: transform(212),                   # Illusion Food - Chick: chick.adr
    1914: transform(212),                   # Chicken Feed: "It'll make you feel younger!"
    1976: transform(3427),                  # Illusion Food - Deer: deer_bambi.adr
    1977: transform(376), 358: transform(376),   # Illusion Food - Treeble, Treeble Treat: treeble.adr
    1980: transform(434),                   # Animated Snacks: maneatingplant_greenpurple.adr
    1991: transform(88), 1928: transform(88),    # Illusion Food - Man-Eating Plant, Man-Eating Plant Mask
    1917: transform(88),                    # Audrey's Special Salad: "You are what you eat..."
    3003: transform(433),                   # Illusion Food - Man-Eating Plant (Brown/Pink)
    1981: transform(219),                   # Honey Soup: bixie_m_gatherer.adr
    3411: transform(218),                   # Royal Bixie Trinket: bixie_m_elite_guard.adr
    1984: transform(1924),                  # Illusion Food - Spider: spider_m.adr
    1988: transform(28), 1913: transform(28),    # Illusion Food - Bat, Batwing Cookie: bat_01.adr
    3006: transform(379),                   # Giant Bat Illusion Scroll: screecher_boss.adr (Greater Screecher)
    1989: transform(129),                   # Illusion Food - Owl: birds_owl.adr
    1993: transform(62),                    # Illusion Food - Bear Vine Golem: bear_vinegolem.adr
    3069: transform(235),                   # Illusion Food - Fungal Vinegolem Bear: bear_vinegolem_fungus.adr
    1996: transform(134),                   # Illusion Food - Floren: floren.adr
    1999: transform(16), 1918: transform(16),    # Illusion Food - Penguin, Ice Cream Sandwich: penguin_01.adr
    1930: transform(47),                    # Penguin Feather Mask: penguin_baby_01.adr
    2028: transform(140),                   # Evil Snowman Illusion Scroll: snowman.adr
    2029: transform(343),                   # Illusion Food - Vulture: vulture.adr
    2030: transform(135),                   # Goat Cheese: billygoat.adr
    3048: transform(136),                   # Illusion Food - Nanny Goat: nannygoat.adr
    3000: transform(74),                    # Arboreal Giant Illusion Scroll: treesoldier.adr
    3007: transform(126), 3008: transform(127), 3009: transform(128), 3010: transform(198),   # Dove, Eagle, Hawk, Raven
    3039: transform(130), 3040: transform(132), 3041: transform(133),   # Antelope, Buck, Doe
    3049: transform(138), 3050: transform(139),  # Snow Hog, Wart Hog
    3054: transform(188),                   # Illusion Food - Robgoblin Female Mage: robgoblin_f_mage.adr
    1927: transform(187),                   # Robgoblin Cooking Mask: robgoblin_f_cook.adr
    359: transform(4), 1921: transform(4),  # Robgoblin Goody, Burnt Toast: robgoblin_m_basic.adr
    1923: transform(199),                   # Bandit Burger: chugawug_m_bandit_01.adr
    252: transform(1667, 15 * MINUTE),      # Crustay Ocean Cake: "into a crab for 15 minutes"
    653: transform(1521), 654: transform(1516),  # Coyote / Road Runner Juice Bottle
    3712: transform(4470),                  # Bug Me Potion: chugawug_m_bugawug_01.adr

    # Fireworks
    4066: cast(15656), 4067: cast(15657), 4068: cast(15658), 4069: cast(15659), 4070: cast(15660),   # Pinwheels
    4071: cast(15664),                      # Golden Fountain Firework
    4072: cast(15661),                      # Stars and Stripes Firework
    3786: cast(15717),                      # Booming Blue Firecrackers
    3788: cast(15731),                      # Red Dragon Firecrackers
    3789: cast(15727),                      # Skylightning Firecrackers
    3955: cast(15729),                      # Queen's Firecrackers
    345: cast(16517),                       # Birthday Firework
    346: cast(16519),                       # Anniversary Firework
    347: cast(16518),                       # Free Realms Firework: "It really says, "Free Realms"."
}

# Families matched on the description. The stats are the ones AbilityEffectTypeSettings.txt shows for
# these tooltips: Talent (+ #) reads M3SpecialSpawnRate, Timing reads M3Timer, Attack reads
# EquippedMeleeWeaponDamage, Health/Energy read MaxHealth/MaxMana.
DESCRIPTION_RULES = [
    (re.compile(r"increases your talent", re.I), lambda: stats("Talent", FOOD_DURATION_MS, POWERUP_EFFECT, M3SpecialSpawnRate=2)),
    (re.compile(r"increases your timing|buffs your timing", re.I), lambda: stats("Timing", FOOD_DURATION_MS, POWERUP_EFFECT, M3Timer=2)),
    (re.compile(r"increases your attack", re.I), lambda: stats("Attack", FOOD_DURATION_MS, POWERUP_EFFECT, EquippedMeleeWeaponDamage=20)),
    (re.compile(r"temporarily increases your health", re.I), lambda: stats("MaxHealth", FOOD_DURATION_MS, POWERUP_EFFECT, MaxHealth=250)),
    (re.compile(r"temporarily increases your energy", re.I), lambda: stats("MaxEnergy", FOOD_DURATION_MS, POWERUP_EFFECT, MaxMana=25)),
]

# The (deprecated) combat scrolls name each stat they buff.
SCROLL_STATS = [
    (re.compile(r"chance to critically hit", re.I), {"MeleeCriticalHitChance": 5, "AbilityCriticalHitChance": 5}),
    (re.compile(r"critical strike damage", re.I), {"MeleeCriticalHitMultiplier": 0.25, "AbilityCriticalHitMultiplier": 0.25}),
    (re.compile(r"player's damage", re.I), {"DamageAddition": 20}),
    (re.compile(r"player's health", re.I), {"MaxHealth": 250}),
    (re.compile(r"player's (the player's )?mana", re.I), {"MaxMana": 25}),
]

LOOP_NAME = re.compile(r"_loop", re.I)


def load_effect_names(client_dir):
    root = ET.parse(client_dir / "Resources" / "ActorCompositeEffectDefinitions.xml").getroot()
    return {int(effect.get("id")): effect.get("name") for effect in root.iter("EffectDefinition")}


def scroll_ability(description):
    values = {}
    for pattern, stat_values in SCROLL_STATS:
        if pattern.search(description):
            values.update(stat_values)
    return stats("CombatScroll", FOOD_DURATION_MS, POWERUP_EFFECT, **values) if values else None


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--client", type=Path, required=True, help="Free Realms client folder (contains Locale and Resources)")
    args = parser.parse_args()

    locale = load_locale(args.client)
    text = lambda string_id: locale.get(lookup2(f"Global.Text.{string_id}".encode()), "") if string_id else ""
    effect_names = load_effect_names(args.client)
    model_ids = {int(line.split("^", 1)[0]) for line in (resources_dir / "Models.txt").read_text("utf-8-sig").splitlines()
                 if line[:1].isdigit()}

    consumables = json.loads((resources_dir / "Consumables.json").read_text("utf-8-sig"))
    claimed = {entry["AbilityId"] for key in ("FoodEffects", "Transformations") for entry in consumables[key]}

    items_by_ability = defaultdict(list)
    for item in json.loads((resources_dir / "ClientItemDefinitions.json").read_text("utf-8-sig")):
        if item.get("ActivatableAbilityId"):
            items_by_ability[item["ActivatableAbilityId"]].append(item)

    abilities = []
    for ability_id, items in sorted(items_by_ability.items()):
        if ability_id in claimed:
            continue

        description = text(items[0].get("DescriptionId", 0))
        ability = ABILITIES.get(ability_id)

        if ability is None and "DEPRECATED" in (items[0].get("Comment") or "") and "scroll" in description.lower():
            ability = scroll_ability(description)

        if ability is None:
            ability = next((build() for pattern, build in DESCRIPTION_RULES if pattern.search(description)), None)

        if ability is None:
            continue

        ability = dict(ability)

        for key in ("CastEffectId", "AuraEffectId"):
            effect_id = ability.get(key, 0)
            if effect_id and effect_id not in effect_names:
                raise SystemExit(f"Ability {ability_id}: composite effect {effect_id} is not in ActorCompositeEffectDefinitions.xml")

        if ability.get("ModelId", 0) not in model_ids | {0}:
            raise SystemExit(f"Ability {ability_id}: model {ability['ModelId']} is not in Models.txt")

        # A looping effect sent as a one-shot never stops on its own, so time it as an aura instead.
        cast_effect = ability.get("CastEffectId", 0)
        if cast_effect and LOOP_NAME.search(effect_names[cast_effect]) and not ability.get("AuraEffectId"):
            ability["AuraEffectId"] = ability.pop("CastEffectId")
            ability.setdefault("DurationMs", LOOP_CAST_DURATION_MS)

        names = sorted({text(item.get("NameId", 0)) or item.get("Comment") or str(item["Id"]) for item in items})
        abilities.append({"AbilityId": ability_id, "Comment": ", ".join(names), **ability})

    output = resources_dir / "ItemAbilities.json"
    output.write_text(json.dumps(abilities, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(abilities)} item abilities to {output}")


if __name__ == "__main__":
    main()
