using System.Numerics;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.MiniGames.CombatEncounters;

public sealed class FrostfangGrowlerEncounter : CombatEncounter
{
    public const int FrostfangGrowlerGameId = 174;

    private const int EncounterZoneDefinitionId = 200;
    private const float EncounterPositionX = 130.108f;
    private const float EncounterPositionY = 0.688f;
    private const float EncounterPositionZ = 120.036f;
    private const float EncounterPositionW = 1f;
    private const float EncounterRotationX = 0.2211563f;
    private const float EncounterRotationZ = 0.9752384f;

    private const int RewardBundleUnknown3 = 15;
    private const int NoTintId = -1;

    private const int DefaultEncounterDefinitionId = 1008;
    private const int FrostfangGrowlerNameId = 93276;
    private const int FrostfangGrowlerIconId = 28605;
    private const int FrostfangGrowlerDescriptionId = 104171;
    private const int FrostfangGrowlerDifficulty = 1;
    private const int CombatMiniGameType = 4;
    private const int FrostfangGrowlerProfileType = 2;
    private const int FrostfangGrowlerDurationMs = 2000;
    private const int FrostfangGrowlerRespawnTime = 10000;

    private const int CoinsRewardIconId = 102;
    private const int CoinsRewardNameId = 6513;
    private const int FrostfangGrowlerMemberCoins = 36;
    private const int FrostfangGrowlerMemberRewardDescriptionId = 3796;

    private const int FrostfangGrowlerPreviewCoins = 20;
    private const int FrostfangGrowlerPreviewIconId = 2483;
    private const int FrostfangGrowlerPreviewNameId = 133217;
    private const int FrostfangGrowlerPreviewDescriptionId = 957;

    private const int PreviewItem1DefinitionId = 76209;
    private const int PreviewItem1IconId = 2483;
    private const int PreviewItem1TintId = 234;
    private const int PreviewItem1NameId = 133217;
    private const int PreviewItem2DefinitionId = 75408;
    private const int PreviewItem2IconId = 3717;
    private const int PreviewItem2TintId = 264;
    private const int PreviewItem2NameId = 131152;
    private const int PreviewItem3DefinitionId = 75091;
    private const int PreviewItem3IconId = 3229;
    private const int PreviewItem3TintId = 247;
    private const int PreviewItem3NameId = 131975;
    private const int PreviewItem4DefinitionId = 75385;
    private const int PreviewItem4IconId = 1198;
    private const int PreviewItem4NameId = 131129;

    private const int NinjaProfileId = 2;
    private const int ArcherProfileId = 35;
    private const int ArcherPowerShardOfVitalityDefinitionId = 75224;
    private const int ArcherPowerShardOfVitalityIconId = 3721;
    private const int ArcherPowerShardOfVitalityTintId = 230;
    private const int ArcherPowerShardOfVitalityNameId = 130968;
    private const int ArcherRingOfRegenerationDefinitionId = 75180;
    private const int ArcherRingOfRegenerationIconId = 547;
    private const int ArcherRingOfRegenerationNameId = 130924;
    private const int ArcherBowOfVolleysDefinitionId = 75000;
    private const int ArcherBowOfVolleysIconId = 3104;
    private const int ArcherBowOfVolleysTintId = 228;
    private const int ArcherBowOfVolleysNameId = 131884;
    private const int MedicProfileId = 11;
    private const int MedicPowerShardOfRegenerationDefinitionId = 75343;
    private const int MedicPowerShardOfRegenerationIconId = 3717;
    private const int MedicPowerShardOfRegenerationTintId = 232;
    private const int MedicPowerShardOfRegenerationNameId = 131087;
    private const int MedicReflexHammerOfVitalsDefinitionId = 75061;
    private const int MedicReflexHammerOfVitalsIconId = 3133;
    private const int MedicReflexHammerOfVitalsTintId = 232;
    private const int MedicReflexHammerOfVitalsNameId = 131945;
    private const int MedicNecklaceOfVitalityDefinitionId = 75320;
    private const int MedicNecklaceOfVitalityIconId = 1198;
    private const int MedicNecklaceOfVitalityNameId = 131064;
    private const int WizardProfileId = 12;
    private const int WizardPowerShardOfRegenerationDefinitionId = 75538;
    private const int WizardPowerShardOfRegenerationIconId = 3717;
    private const int WizardPowerShardOfRegenerationTintId = 264;
    private const int WizardPowerShardOfRegenerationNameId = 131282;
    private const int WizardSparkleTwigOfGlaciersDefinitionId = 75151;
    private const int WizardSparkleTwigOfGlaciersIconId = 3158;
    private const int WizardSparkleTwigOfGlaciersTintId = 247;
    private const int WizardSparkleTwigOfGlaciersNameId = 132035;
    private const int WizardNecklaceOfVitalityDefinitionId = 75515;
    private const int WizardNecklaceOfVitalityIconId = 1198;
    private const int WizardNecklaceOfVitalityNameId = 131259;
    private const int WarriorProfileId = 32;
    private const int WarriorPowerShardOfRegenerationDefinitionId = 75473;
    private const int WarriorPowerShardOfRegenerationIconId = 3717;
    private const int WarriorPowerShardOfRegenerationTintId = 232;
    private const int WarriorPowerShardOfRegenerationNameId = 131217;
    private const int WarriorCudgelOfCleavingDefinitionId = 75121;
    private const int WarriorCudgelOfCleavingIconId = 3120;
    private const int WarriorCudgelOfCleavingTintId = 232;
    private const int WarriorCudgelOfCleavingNameId = 132005;
    private const int WarriorNecklaceOfVitalityDefinitionId = 75450;
    private const int WarriorNecklaceOfVitalityIconId = 1198;
    private const int WarriorNecklaceOfVitalityNameId = 131194;
    private const int BrawlerProfileId = 43;
    private const int BrawlerPowerShardOfRegenerationDefinitionId = 75278;
    private const int BrawlerPowerShardOfRegenerationIconId = 3717;
    private const int BrawlerPowerShardOfRegenerationTintId = 264;
    private const int BrawlerPowerShardOfRegenerationNameId = 131022;
    private const int BrawlerMalletOfRumblingDefinitionId = 75031;
    private const int BrawlerMalletOfRumblingIconId = 3131;
    private const int BrawlerMalletOfRumblingTintId = 247;
    private const int BrawlerMalletOfRumblingNameId = 131915;
    private const int BrawlerNecklaceOfVitalityDefinitionId = 75255;
    private const int BrawlerNecklaceOfVitalityIconId = 1198;
    private const int BrawlerNecklaceOfVitalityNameId = 130999;
    private const int BattleItemMysteryPackDefinitionId = 10482;
    private const int BattleItemMysteryPackIconId = 973;
    private const int BattleItemMysteryPackNameId = 6666;

    private const int KnockOutObjectiveId = 12288;
    private const int KnockOutObjectiveNameId = 2286;
    private const int KnockOutObjectiveDescriptionId = 100712;
    private const int KnockOutObjectiveTotal = 5;

    private const int ScareWolvesObjectiveId = 12642;
    private const int ScareWolvesObjectiveNameId = 104176;
    private const int ScareWolvesObjectiveDescriptionId = 104177;
    private const int ScareWolvesObjectiveTotal = 1;
    private const int ScareWolvesObjectiveCompleteValue = 5000;
    private const int StarRewardExperience = 10;
    private const int StarRewardIconId = 3901;
    private const int StarRewardNameId = 2416;
    private const int StarRewardDescriptionId = 5240;

    private const int CombatGameTimeOverride = -1;
    private const float CombatObjectiveMultiplier = 500f;
    private const int CombatQuickCompletionTime = 1;
    private const int CombatQuickCompletionValue = 100000;
    private const int CombatQuickCompletionLoss = 500;
    private const int CombatNpcKOValue = 300;
    private const int CombatKnockoutRewardValue = 25000;
    private const int CombatKnockoutPenalty = 5000;

    public override MiniGameUiDefinition Definition { get; } = CreateDefinition();

    public override int ZoneDefinitionId => EncounterZoneDefinitionId;

    public override Vector4 EntryPosition { get; } = new(EncounterPositionX, EncounterPositionY, EncounterPositionZ, EncounterPositionW);

    public override Quaternion EntryRotation { get; } = new(EncounterRotationX, default, EncounterRotationZ, default);

    private static MiniGameUiDefinition CreateDefinition()
    {
        var memberRewardBundle = CreateRewardBundle(CoinsRewardIconId, CoinsRewardNameId);
        memberRewardBundle.HasOptionalData = true;
        memberRewardBundle.Coins = FrostfangGrowlerMemberCoins;
        memberRewardBundle.DescriptionId = FrostfangGrowlerMemberRewardDescriptionId;

        var previewRewardBundle = CreateRewardBundle(FrostfangGrowlerPreviewIconId, FrostfangGrowlerPreviewNameId);
        previewRewardBundle.Coins = FrostfangGrowlerPreviewCoins;
        previewRewardBundle.DescriptionId = FrostfangGrowlerPreviewDescriptionId;
        previewRewardBundle.Entries.Add(CreateRewardItem(PreviewItem1DefinitionId, PreviewItem1IconId, PreviewItem1TintId, PreviewItem1NameId, true));
        previewRewardBundle.Entries.Add(CreateRewardItem(PreviewItem2DefinitionId, PreviewItem2IconId, PreviewItem2TintId, PreviewItem2NameId, false));
        previewRewardBundle.Entries.Add(CreateRewardItem(PreviewItem3DefinitionId, PreviewItem3IconId, PreviewItem3TintId, PreviewItem3NameId, false));
        previewRewardBundle.Entries.Add(CreateRewardItem(PreviewItem4DefinitionId, PreviewItem4IconId, default, PreviewItem4NameId, false));
        previewRewardBundle.Entries.Add(CreateRewardItem(BattleItemMysteryPackDefinitionId, BattleItemMysteryPackIconId, NoTintId, BattleItemMysteryPackNameId, true));

        var starRewardBundle = CreateRewardBundle(StarRewardIconId, StarRewardNameId);
        starRewardBundle.Experience = StarRewardExperience;
        starRewardBundle.DescriptionId = StarRewardDescriptionId;

        return new MiniGameUiDefinition
        {
            GameId = FrostfangGrowlerGameId,
            EncounterDefinitionId = DefaultEncounterDefinitionId,
            NameId = FrostfangGrowlerNameId,
            IconId = FrostfangGrowlerIconId,
            DescriptionId = FrostfangGrowlerDescriptionId,
            Difficulty = FrostfangGrowlerDifficulty,
            Type = CombatMiniGameType,
            ProfileType = FrostfangGrowlerProfileType,
            DurationMs = FrostfangGrowlerDurationMs,
            RespawnTime = FrostfangGrowlerRespawnTime,
            ShowStarCounter = true,
            ShowStatusIcon = true,
            ShowActionBar = true,
            StartupInCombat = true,
            ShowEndDialog = true,
            RewardBundle = RewardBundleBase.CreateEmpty(),
            MemberRewardBundle = memberRewardBundle,
            PreviewRewardBundle = previewRewardBundle,
            ProfilePreviewRewardBundles = new()
            {
                [NinjaProfileId] = previewRewardBundle,
                [ArcherProfileId] = CreatePreviewRewardBundle(
                    CreateRewardItem(ArcherPowerShardOfVitalityDefinitionId, ArcherPowerShardOfVitalityIconId, ArcherPowerShardOfVitalityTintId, ArcherPowerShardOfVitalityNameId, false),
                    CreateRewardItem(ArcherRingOfRegenerationDefinitionId, ArcherRingOfRegenerationIconId, default, ArcherRingOfRegenerationNameId, false),
                    CreateRewardItem(ArcherBowOfVolleysDefinitionId, ArcherBowOfVolleysIconId, ArcherBowOfVolleysTintId, ArcherBowOfVolleysNameId, false)),
                [MedicProfileId] = CreatePreviewRewardBundle(
                    CreateRewardItem(MedicPowerShardOfRegenerationDefinitionId, MedicPowerShardOfRegenerationIconId, MedicPowerShardOfRegenerationTintId, MedicPowerShardOfRegenerationNameId, false),
                    CreateRewardItem(MedicReflexHammerOfVitalsDefinitionId, MedicReflexHammerOfVitalsIconId, MedicReflexHammerOfVitalsTintId, MedicReflexHammerOfVitalsNameId, false),
                    CreateRewardItem(MedicNecklaceOfVitalityDefinitionId, MedicNecklaceOfVitalityIconId, default, MedicNecklaceOfVitalityNameId, false)),
                [WizardProfileId] = CreatePreviewRewardBundle(
                    CreateRewardItem(WizardPowerShardOfRegenerationDefinitionId, WizardPowerShardOfRegenerationIconId, WizardPowerShardOfRegenerationTintId, WizardPowerShardOfRegenerationNameId, false),
                    CreateRewardItem(WizardSparkleTwigOfGlaciersDefinitionId, WizardSparkleTwigOfGlaciersIconId, WizardSparkleTwigOfGlaciersTintId, WizardSparkleTwigOfGlaciersNameId, false),
                    CreateRewardItem(WizardNecklaceOfVitalityDefinitionId, WizardNecklaceOfVitalityIconId, default, WizardNecklaceOfVitalityNameId, false)),
                [WarriorProfileId] = CreatePreviewRewardBundle(
                    CreateRewardItem(WarriorPowerShardOfRegenerationDefinitionId, WarriorPowerShardOfRegenerationIconId, WarriorPowerShardOfRegenerationTintId, WarriorPowerShardOfRegenerationNameId, false),
                    CreateRewardItem(WarriorCudgelOfCleavingDefinitionId, WarriorCudgelOfCleavingIconId, WarriorCudgelOfCleavingTintId, WarriorCudgelOfCleavingNameId, false),
                    CreateRewardItem(WarriorNecklaceOfVitalityDefinitionId, WarriorNecklaceOfVitalityIconId, default, WarriorNecklaceOfVitalityNameId, false)),
                [BrawlerProfileId] = CreatePreviewRewardBundle(
                    CreateRewardItem(BrawlerPowerShardOfRegenerationDefinitionId, BrawlerPowerShardOfRegenerationIconId, BrawlerPowerShardOfRegenerationTintId, BrawlerPowerShardOfRegenerationNameId, false),
                    CreateRewardItem(BrawlerMalletOfRumblingDefinitionId, BrawlerMalletOfRumblingIconId, BrawlerMalletOfRumblingTintId, BrawlerMalletOfRumblingNameId, false),
                    CreateRewardItem(BrawlerNecklaceOfVitalityDefinitionId, BrawlerNecklaceOfVitalityIconId, default, BrawlerNecklaceOfVitalityNameId, false))
            },
            Objectives =
            [
                new MiniGameUiObjective
                {
                    Id = KnockOutObjectiveId,
                    NameId = KnockOutObjectiveNameId,
                    DescriptionId = KnockOutObjectiveDescriptionId,
                    Total = KnockOutObjectiveTotal,
                    IsFailCondition = true,
                    RewardBundle = RewardBundleBase.CreateEmpty()
                },
                new MiniGameUiObjective
                {
                    Id = ScareWolvesObjectiveId,
                    NameId = ScareWolvesObjectiveNameId,
                    DescriptionId = ScareWolvesObjectiveDescriptionId,
                    Total = ScareWolvesObjectiveTotal,
                    CompleteValue = ScareWolvesObjectiveCompleteValue,
                    ShowTask = true,
                    RewardBundle = starRewardBundle
                }
            ],
            EncounterDataCommon = new EncounterDataCommon
            {
                CombatStance = true,
                CanTakeDamage = true,
                CanUseAbilities = true,
                CanUseConsumables = true,
                CanMelee = true,
                CanUseTraits = true,
                CanChangeMaxMoveSpeed = true,
                ShowHpBar = true,
                ShowCombatUi = true,
                UseCombatCamera = true,
                CombatType = true,
                GameTimeOverride = CombatGameTimeOverride,
                ObjectiveMultiplier = CombatObjectiveMultiplier,
                QuickCompletionTime = CombatQuickCompletionTime,
                QuickCompletionValue = CombatQuickCompletionValue,
                QuickCompletionLoss = CombatQuickCompletionLoss,
                NpcKOValue = CombatNpcKOValue,
                KnockoutRewardValue = CombatKnockoutRewardValue,
                KnockoutPenalty = CombatKnockoutPenalty
            }
        };
    }

    private static RewardBundleBase CreateRewardBundle(int iconId, int nameId)
    {
        return new RewardBundleBase
        {
            HasOptionalData = false,
            Unknown3 = RewardBundleUnknown3,
            IconId = iconId,
            NameId = nameId
        };
    }

    private static RewardBundleBase CreatePreviewRewardBundle(params RewardBundleEntryItem[] rewardBundleEntryItems)
    {
        var previewRewardBundle = CreateRewardBundle(rewardBundleEntryItems[0].IconId, rewardBundleEntryItems[0].NameId);
        previewRewardBundle.Coins = FrostfangGrowlerPreviewCoins;
        previewRewardBundle.DescriptionId = FrostfangGrowlerPreviewDescriptionId;
        previewRewardBundle.Entries.AddRange(rewardBundleEntryItems);
        previewRewardBundle.Entries.Add(CreateRewardItem(BattleItemMysteryPackDefinitionId, BattleItemMysteryPackIconId, NoTintId, BattleItemMysteryPackNameId, true));

        return previewRewardBundle;
    }

    private static RewardBundleEntryItem CreateRewardItem(int definitionId, int iconId, int tintId, int nameId, bool isHidden)
    {
        return new RewardBundleEntryItem
        {
            IsHidden = isHidden,
            IconId = iconId,
            TintId = tintId,
            NameId = nameId,
            DefinitionId = definitionId
        };
    }
}
