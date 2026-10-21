using System.Collections.Generic;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.MiniGames;

public static class SignUpMiniGameDefinitions
{
    private const int RetailEncounterDefinitionId = 1008;
    private const int RetailDurationMs = 2000;
    private const int RetailRespawnTime = 10000;
    private const int RetailDifficulty = 1;
    private const int NoRewardIconId = -1;
    private const int NoRewardNameId = -1;
    private const float DefaultRewardMultiplier = 1f;

    private const int DemolitionDerbyActivityId = 65;
    private const int DemolitionDerbyDetailsType = 3;
    private const int DemolitionDerbyNameId = 37938;
    private const int DemolitionDerbyIconId = 9869;
    private const int DemolitionDerbyDescriptionId = 37941;
    private const int DemolitionDerbyMiniGameType = 11;
    private const int DemolitionDerbyMiniGameId = 39;

    private const int KartRaceActivityId = 62;
    private const int KartRaceDetailsType = 2;
    private const int KartRaceNameId = 17958;
    private const int KartRaceIconId = 9870;
    private const int KartRaceDescriptionId = 37935;
    private const int KartRaceMiniGameType = 10;
    private const int KartRaceMiniGameId = 28;

    private const int SoccerActivityId = 72;
    private const int SoccerDetailsType = 4;
    private const int SoccerNameId = 37944;
    private const int SoccerIconId = 20992;
    private const int SoccerDescriptionId = 6703;
    private const int SoccerMiniGameType = 12;
    private const int SoccerMiniGameId = 61;
    private const int SoccerObjectiveId = 2350;
    private const int SoccerObjectiveNameId = 20136;
    private const int SoccerObjectiveDescriptionId = 20137;

    public static IReadOnlyList<MiniGameUiDefinition> Definitions { get; } =
    [
        Create(DemolitionDerbyActivityId, DemolitionDerbyDetailsType, DemolitionDerbyNameId, DemolitionDerbyIconId, DemolitionDerbyDescriptionId, DemolitionDerbyMiniGameType, DemolitionDerbyMiniGameId, []),
        Create(KartRaceActivityId, KartRaceDetailsType, KartRaceNameId, KartRaceIconId, KartRaceDescriptionId, KartRaceMiniGameType, KartRaceMiniGameId, []),
        Create(SoccerActivityId, SoccerDetailsType, SoccerNameId, SoccerIconId, SoccerDescriptionId, SoccerMiniGameType, SoccerMiniGameId,
        [
            new MiniGameUiObjective
            {
                Id = SoccerObjectiveId,
                NameId = SoccerObjectiveNameId,
                DescriptionId = SoccerObjectiveDescriptionId,
                RewardBundle = CreateEmptyRewardBundle()
            }
        ])
    ];

    private static MiniGameUiDefinition Create(int activityId, int detailsType, int nameId, int iconId, int descriptionId, int miniGameType, int miniGameId, List<MiniGameUiObjective> objectives)
    {
        return new MiniGameUiDefinition
        {
            GameId = activityId,
            EncounterDefinitionId = RetailEncounterDefinitionId,
            DetailsType = detailsType,
            DetailsUnknown3 = false,
            PreselectedGameId = miniGameId,
            NameId = nameId,
            IconId = iconId,
            DescriptionId = descriptionId,
            Difficulty = RetailDifficulty,
            Type = miniGameType,
            DurationMs = RetailDurationMs,
            RespawnTime = RetailRespawnTime,
            ShowStarCounter = true,
            ShowStatusIcon = true,
            ShowActionBar = true,
            StartupInCombat = true,
            ShowEndDialog = true,
            RewardBundle = CreateEmptyRewardBundle(),
            MemberRewardBundle = CreateEmptyRewardBundle(),
            PreviewRewardBundle = CreateEmptyRewardBundle(),
            Objectives = objectives
        };
    }

    private static RewardBundleInfo CreateEmptyRewardBundle()
    {
        return new RewardBundleInfo
        {
            Multiplier = DefaultRewardMultiplier,
            IconId = NoRewardIconId,
            NameId = NoRewardNameId
        };
    }
}
