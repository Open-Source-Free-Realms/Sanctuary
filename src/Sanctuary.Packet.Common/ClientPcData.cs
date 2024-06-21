using System;
using System.Numerics;
using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientPcData
{
    /// <summary>
    /// Used for TCG.
    /// </summary>
    public ulong LaunchTicket;
    public ulong Guid { get; init; }

    public int Model;

    public string Head = null!;
    public string Hair = null!;

    public int HairColor;
    public int EyeColor;

    public string SkinTone = null!;

    public string? FacePaint;
    public string? ModelCustomization;

    public int HeadId;
    public int HairId;
    public int SkinToneId;
    public int FacePaintId;
    public int ModelCustomizationId;

    public Vector4 Position { get; set; }
    public Quaternion Rotation { get; set; }

    public NameData Name = new();

    public int Coins;

    public DateTimeOffset Birthday;
    public int Age;
    public int PlayTime;

    public bool IsUnderage;
    public bool IsOpenChatEnabled;

    // 0 - Inactive
    // 1 - Trial
    // 2 - Active
    public int MembershipStatus;

    // Only shows when membership isn't active.
    public bool ShowMemberNagScreen;

    // Country Ids
    //   1 - AD
    //   2 - AE
    //   3 - AF
    //   4 - AG
    //   5 - AI
    //   6 - AL
    //   7 - AM
    //   8 - AN
    //   9 - AO
    //  10 - AQ
    //  11 - AR
    //  12 - AS
    //  13 - AT
    //  14 - AU
    //  15 - AW
    //  16 - AZ
    //  17 - BA
    //  18 - BB
    //  19 - BD
    //  20 - BE
    //  21 - BF
    //  22 - BG
    //  23 - BH
    //  24 - BI
    //  25 - BJ
    //  26 - BM
    //  27 - BN
    //  28 - BO
    //  29 - BR
    //  30 - BS
    //  31 - BT
    //  32 - BV
    //  33 - BW
    //  34 - BY
    //  35 - BZ
    //  36 - CA
    //  37 - CC
    //  38 - CD
    //  39 - CF
    //  40 - CG
    //  41 - CH
    //  42 - CI
    //  43 - CK
    //  44 - CL
    //  45 - CM
    //  46 - CN
    //  47 - CO
    //  48 - CR
    //  49 - CV
    //  50 - CX
    //  51 - CY
    //  52 - CZ
    //  53 - DE
    //  54 - DJ
    //  55 - DK
    //  56 - DM
    //  57 - DO
    //  58 - DZ
    //  59 - EC
    //  60 - EE
    //  61 - EG
    //  62 - EH
    //  63 - ER
    //  64 - ES
    //  65 - ET
    //  66 - FI
    //  67 - FJ
    //  68 - FK
    //  69 - FM
    //  70 - FO
    //  71 - FR
    //  72 - GA
    //  73 - GB
    //  74 - GD
    //  75 - GE
    //  76 - GF
    //  77 - GH
    //  78 - GI
    //  79 - GL
    //  80 - GM
    //  81 - GN
    //  82 - GP
    //  83 - GQ
    //  84 - GR
    //  85 - GS
    //  86 - GT
    //  87 - GU
    //  88 - GW
    //  89 - GY
    //  90 - HK
    //  91 - HM
    //  92 - HN
    //  93 - HR
    //  94 - HT
    //  95 - HU
    //  96 - ID
    //  97 - IE
    //  98 - IL
    //  99 - IN
    // 100 - IO
    // 101 - IQ
    // 102 - IS
    // 103 - IT
    // 104 - JM
    // 105 - JO
    // 106 - JP
    // 107 - KE
    // 108 - KG
    // 109 - KH
    // 110 - KI
    // 111 - KM
    // 112 - KN
    // 113 - KR
    // 114 - KW
    // 115 - KY
    // 116 - KZ
    // 117 - LA
    // 118 - LB
    // 119 - LC
    // 120 - LI
    // 121 - LK
    // 122 - LR
    // 123 - LS
    // 124 - LT
    // 125 - LU
    // 126 - LV
    // 127 - LY
    // 128 - MA
    // 129 - MC
    // 130 - MD
    // 131 - MG
    // 132 - MH
    // 133 - MK
    // 134 - ML
    // 135 - MM
    // 136 - MN
    // 137 - MO
    // 138 - MP
    // 139 - MQ
    // 140 - MR
    // 141 - MS
    // 142 - MT
    // 143 - MU
    // 144 - MV
    // 145 - MW
    // 146 - MX
    // 147 - MY
    // 148 - MZ
    // 149 - NA
    // 150 - NC
    // 151 - NE
    // 152 - NF
    // 153 - NG
    // 154 - NI
    // 155 - NL
    // 156 - NO
    // 157 - NP
    // 158 - NR
    // 159 - NU
    // 160 - NZ
    // 161 - OM
    // 162 - PA
    // 163 - PE
    // 164 - PF
    // 165 - PG
    // 166 - PH
    // 167 - PK
    // 168 - PL
    // 169 - PM
    // 170 - PN
    // 171 - PR
    // 172 - PS
    // 173 - PT
    // 174 - PW
    // 175 - PY
    // 176 - QA
    // 177 - RE
    // 178 - RO
    // 179 - RU
    // 180 - RW
    // 181 - SA
    // 182 - SB
    // 183 - SC
    // 184 - SE
    // 185 - SG
    // 186 - SH
    // 187 - SI
    // 188 - SJ
    // 189 - SK
    // 190 - SL
    // 191 - SM
    // 192 - SN
    // 193 - SO
    // 194 - SR
    // 195 - ST
    // 196 - SV
    // 197 - SZ
    // 198 - TC
    // 199 - TD
    // 200 - TF
    // 201 - TG
    // 202 - TH
    // 203 - TJ
    // 204 - TK
    // 205 - TM
    // 206 - TN
    // 207 - TO
    // 208 - TP
    // 209 - TR
    // 210 - TT
    // 211 - TV
    // 212 - TW
    // 213 - TZ
    // 214 - UA
    // 215 - UG
    // 216 - UM
    // 217 - US (Client Default)
    // 218 - UY
    // 219 - UZ
    // 220 - VA
    // 221 - VC
    // 222 - VE
    // 223 - VG
    // 224 - VI
    // 225 - VN
    // 226 - VU
    // 227 - WF
    // 228 - WS
    // 229 - YE
    // 230 - YT
    // 231 - YU
    // 232 - ZA
    // 233 - ZM
    // 234 - ZW

    public int ChatCountryId;

    //  1 - de
    //  2 - en (Client Default)
    //  3 - es
    //  4 - fr
    //  5 - it
    //  6 - ja
    //  7 - ko
    //  8 - pt
    //  9 - ru
    // 10 - sv
    // 11 - zh

    public int ChatLanguageId;

    // For the `Options > Chat Settings > My Languages`.
    // Stored in bits, based on the language id in `Languages.txt`.

    public int PreferredLanguage;

    // This variable is only set by the client `ClientPcData::SetChatLanguageId`
    public int ChatLanguage;

    public int LoginCount;

    public bool Grandfathered;

    public int ActiveVehicleLoadout_KartRace;
    public int ActiveVehicleLoadout_DemoDerby;

    public List<ClientPcProfile> Profiles = new();

    public int ActiveProfile;

    public List<ProfileTypeEntry> ProfileTypes = new();

    // public List<Collections> Collections = new();

    public List<ClientItem> Items = new();

    public int Gender;

    // public ClientQuestData Quests = new();
    // public ClientAchievementData Achievements = new();

    // public List<Acquaintance> Acquaintances = new();
    // public List<RecipeData> Recipes = new();

    // public List<UnknownPetStruct> Pets = new();

    public int ActivePetId;
    public ulong ActivePetGuid;

    public List<PacketMountInfo> Mounts = new();

    public Dictionary<int, ClientActionBar> ActionBars = new();

    // public List<int> FirstTimeEvent = new();
    // public List<int> MiniGameTutorials = new();

    // public Dictionary<int, ClientEffectTag> EffectTags = new();

    public List<NameChangeInfo> PendingNameChanges = new();

    public Dictionary<CharacterStatId, CharacterStat> Stats = new();

    // public VehicleStruct VehicleStruct = new();

    public List<PlayerTitleData> Titles = new();

    public int ActiveTitle;

    public float VipRank;
    public int VipIconId;
    public int VipTitle;

    // public List<ClientNudgeData> ClientNudges = new();

    public ClientPcData()
    {
        Stats.Add(CharacterStatId.MaxHealth, new(CharacterStatId.MaxHealth, 2500));
        Stats.Add(CharacterStatId.MaxMovementSpeed, new(CharacterStatId.MaxMovementSpeed, 8f));
        Stats.Add(CharacterStatId.WeaponRange, new(CharacterStatId.WeaponRange, 5f));
        Stats.Add(CharacterStatId.HitPointRegen, new(CharacterStatId.HitPointRegen, 25));
        Stats.Add(CharacterStatId.MaxMana, new(CharacterStatId.MaxMana, 100));
        Stats.Add(CharacterStatId.ManaRegen, new(CharacterStatId.ManaRegen, 4));
        Stats.Add(CharacterStatId.Defense, new(CharacterStatId.Defense, 0));
        Stats.Add(CharacterStatId.MeleeAvoidance, new(CharacterStatId.MeleeAvoidance, 0));
        Stats.Add(CharacterStatId.MeleeCriticalHitChance, new(CharacterStatId.MeleeCriticalHitChance, 0));
        Stats.Add(CharacterStatId.MeleeCriticalHitMultiplier, new(CharacterStatId.MeleeCriticalHitMultiplier, 0f));
        Stats.Add(CharacterStatId.MeleeChanceToHit, new(CharacterStatId.MeleeChanceToHit, 100));
        Stats.Add(CharacterStatId.MeleeWeaponDamageMultiplier, new(CharacterStatId.MeleeWeaponDamageMultiplier, 1f));
        Stats.Add(CharacterStatId.MeleeHandToHandDamage, new(CharacterStatId.MeleeHandToHandDamage, 1));
        Stats.Add(CharacterStatId.EquippedMeleeWeaponDamage, new(CharacterStatId.EquippedMeleeWeaponDamage, 1));
        Stats.Add(CharacterStatId.MeleeAttackIntervalMs, new(CharacterStatId.MeleeAttackIntervalMs, 2000));
        Stats.Add(CharacterStatId.DamageReductionAmount, new(CharacterStatId.DamageReductionAmount, 0));
        Stats.Add(CharacterStatId.ExperienceBoostPercent, new(CharacterStatId.ExperienceBoostPercent, 0));
        Stats.Add(CharacterStatId.DamageReductionPercent, new(CharacterStatId.DamageReductionPercent, 0));
        Stats.Add(CharacterStatId.DamageAddition, new(CharacterStatId.DamageAddition, 0));
        Stats.Add(CharacterStatId.DamageMultiplier, new(CharacterStatId.DamageMultiplier, 1f));
        Stats.Add(CharacterStatId.HealingAddition, new(CharacterStatId.HealingAddition, 0));
        Stats.Add(CharacterStatId.HealingMultiplier, new(CharacterStatId.HealingMultiplier, 1f));
        Stats.Add(CharacterStatId.M3CollectableSpawnRate, new(CharacterStatId.M3CollectableSpawnRate, 0));
        Stats.Add(CharacterStatId.M3SpecialSpawnRate, new(CharacterStatId.M3SpecialSpawnRate, 0));
        Stats.Add(CharacterStatId.M3DetrementalSpawnRate, new(CharacterStatId.M3DetrementalSpawnRate, 0));
        Stats.Add(CharacterStatId.M3Magnitude, new(CharacterStatId.M3Magnitude, 0));
        Stats.Add(CharacterStatId.M3Timer, new(CharacterStatId.M3Timer, 0));
        Stats.Add(CharacterStatId.MimicRadius, new(CharacterStatId.MimicRadius, 0));
        Stats.Add(CharacterStatId.MimicCuttingMagnitude, new(CharacterStatId.MimicCuttingMagnitude, 0));
        Stats.Add(CharacterStatId.MimicPowerMagnitude, new(CharacterStatId.MimicPowerMagnitude, 0));
        Stats.Add(CharacterStatId.MimicGreenRange, new(CharacterStatId.MimicGreenRange, 0));
        Stats.Add(CharacterStatId.MimicSpeed, new(CharacterStatId.MimicSpeed, 0));
        Stats.Add(CharacterStatId.AbilityCriticalHitChance, new(CharacterStatId.AbilityCriticalHitChance, 0));
        Stats.Add(CharacterStatId.AbilityCriticalHitMultiplier, new(CharacterStatId.AbilityCriticalHitMultiplier, 1f));
        Stats.Add(CharacterStatId.Luck, new(CharacterStatId.Luck, 0));
        Stats.Add(CharacterStatId.HeadInflationPercent, new(CharacterStatId.HeadInflationPercent, 100));
        Stats.Add(CharacterStatId.GoldBoostPercent, new(CharacterStatId.GoldBoostPercent, 0));
        Stats.Add(CharacterStatId.M3PerMatchProc, new(CharacterStatId.M3PerMatchProc, 0));
        Stats.Add(CharacterStatId.SoccerKickPower, new(CharacterStatId.SoccerKickPower, 0));
        Stats.Add(CharacterStatId.SoccerFootwork, new(CharacterStatId.SoccerFootwork, 0));
        Stats.Add(CharacterStatId.SoccerSpeed, new(CharacterStatId.SoccerSpeed, 0));
        Stats.Add(CharacterStatId.SoccerToughness, new(CharacterStatId.SoccerToughness, 0));
        Stats.Add(CharacterStatId.SoccerTacklePower, new(CharacterStatId.SoccerTacklePower, 0));
        Stats.Add(CharacterStatId.FishingCastingSkill, new(CharacterStatId.FishingCastingSkill, 0));
        Stats.Add(CharacterStatId.FishingCastingStrength, new(CharacterStatId.FishingCastingStrength, 0));
        Stats.Add(CharacterStatId.FishingLineStrength, new(CharacterStatId.FishingLineStrength, 0));
        Stats.Add(CharacterStatId.FishingReelingSpeed, new(CharacterStatId.FishingReelingSpeed, 0));
        Stats.Add(CharacterStatId.FishingLuck, new(CharacterStatId.FishingLuck, 0));
        Stats.Add(CharacterStatId.FishingPerfectCastSkill, new(CharacterStatId.FishingPerfectCastSkill, 0));
        Stats.Add(CharacterStatId.Toughness, new(CharacterStatId.Toughness, 0));
        Stats.Add(CharacterStatId.AbilityCritVulnerability, new(CharacterStatId.AbilityCritVulnerability, 0));
        Stats.Add(CharacterStatId.MeleeCritVulnerability, new(CharacterStatId.MeleeCritVulnerability, 0));
        Stats.Add(CharacterStatId.RangeMultiplier, new(CharacterStatId.RangeMultiplier, 1f));
        Stats.Add(CharacterStatId.MaxShields, new(CharacterStatId.MaxShields, 0));
        Stats.Add(CharacterStatId.ShieldsRegen, new(CharacterStatId.ShieldsRegen, 0));
        Stats.Add(CharacterStatId.FactoryProductionModifier, new(CharacterStatId.FactoryProductionModifier, 1f));
        Stats.Add(CharacterStatId.FactoryYieldModifier, new(CharacterStatId.FactoryYieldModifier, 1f));
        Stats.Add(CharacterStatId.PlayerCastIllusionImmunity, new(CharacterStatId.PlayerCastIllusionImmunity, 0));
        Stats.Add(CharacterStatId.GlideDefaultForwardSpeed, new(CharacterStatId.GlideDefaultForwardSpeed, 0f));
        Stats.Add(CharacterStatId.GlideMinForwardSpeed, new(CharacterStatId.GlideMinForwardSpeed, 0f));
        Stats.Add(CharacterStatId.GlideMaxForwardSpeed, new(CharacterStatId.GlideMaxForwardSpeed, 0f));
        Stats.Add(CharacterStatId.GlideFallTime, new(CharacterStatId.GlideFallTime, 0f));
        Stats.Add(CharacterStatId.GlideFallSpeed, new(CharacterStatId.GlideFallSpeed, 0f));
        Stats.Add(CharacterStatId.GlideEnabled, new(CharacterStatId.GlideEnabled, 0));
        Stats.Add(CharacterStatId.InCombatHitPointRegen, new(CharacterStatId.InCombatHitPointRegen, 6));
        Stats.Add(CharacterStatId.InCombatManaRegen, new(CharacterStatId.InCombatManaRegen, 4));
        Stats.Add(CharacterStatId.GlideAccel, new(CharacterStatId.GlideAccel, 0f));
        Stats.Add(CharacterStatId.JumpHeight, new(CharacterStatId.JumpHeight, 0f));
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(LaunchTicket);
        writer.Write(Guid);

        writer.Write(Model);

        writer.Write(Head);
        writer.Write(Hair);

        writer.Write(HairColor);
        writer.Write(EyeColor);

        writer.Write(SkinTone);

        writer.Write(FacePaint);
        writer.Write(ModelCustomization);

        writer.Write(HeadId);
        writer.Write(HairId);
        writer.Write(SkinToneId);
        writer.Write(FacePaintId);
        writer.Write(ModelCustomizationId);

        writer.Write(Position);
        writer.Write(Rotation);

        Name.Serialize(writer);

        writer.Write(Coins);

        writer.Write(Birthday);
        writer.Write(Age);
        writer.Write(PlayTime);

        writer.Write(IsUnderage);
        writer.Write(IsOpenChatEnabled);

        writer.Write(MembershipStatus);
        writer.Write(ShowMemberNagScreen);

        writer.Write(ChatCountryId);
        writer.Write(ChatLanguageId);
        writer.Write(PreferredLanguage);
        writer.Write(ChatLanguage);

        writer.Write(LoginCount);

        writer.Write(Grandfathered);

        writer.Write(ActiveVehicleLoadout_KartRace);
        writer.Write(ActiveVehicleLoadout_DemoDerby);

        writer.Write(Profiles);

        writer.Write(ActiveProfile);

        writer.Write(ProfileTypes);

        writer.Write(0); // TODO Collections

        writer.Write(Items);

        writer.Write(Gender);

        // TODO Quests
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(false);
        writer.Write(0);
        writer.Write(0);

        // TODO Achievements
        writer.Write(0);
        writer.Write(0);

        writer.Write(0); // TODO Acquaintances

        writer.Write(0); // TODO Recipes

        writer.Write(0); // TODO Pets

        writer.Write(ActivePetId);
        writer.Write(ActivePetGuid);

        writer.Write(Mounts);

        writer.Write(ActionBars);

        // TODO FirstTimeEvent
        writer.Write(true); // Enable
        writer.Write(0);

        writer.Write(0); // TODO MiniGameTutorials
        writer.Write(0); // TODO EffectTags

        writer.Write(PendingNameChanges);

        writer.Write(Stats);

        // TODO VehicleStruct
        writer.Write(0);
        writer.Write(0);

        writer.Write(Titles);

        writer.Write(ActiveTitle);

        writer.Write(VipRank);
        writer.Write(VipIconId);
        writer.Write(VipTitle);

        writer.Write(0); // TODO ClientNudges

        return writer.Buffer;
    }
}