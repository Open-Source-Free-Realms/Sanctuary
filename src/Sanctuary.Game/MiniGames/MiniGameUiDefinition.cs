using System.Collections.Generic;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.MiniGames;

public sealed class MiniGameUiDefinition
{
    private const int CombatEncounterDetailsType = 1;
    private const int DefaultEncounterDefinitionId = 1008;
    private const int DefaultDurationMs = 2000;
    private const int DefaultRespawnTime = 10000;

    public int GameId { get; init; }
    public int EncounterDefinitionId { get; init; } = DefaultEncounterDefinitionId;
    public int DetailsType { get; init; } = CombatEncounterDetailsType;
    public bool DetailsUnknown3 { get; init; } = true;
    public int PreselectedGameId { get; init; }
    public int NameId { get; init; }
    public int IconId { get; init; }
    public int DescriptionId { get; init; }
    public int Difficulty { get; init; }
    public int Type { get; init; }
    public int ProfileType { get; init; }
    public bool MembersOnly { get; init; }
    public int DurationMs { get; init; } = DefaultDurationMs;
    public int RespawnTime { get; init; } = DefaultRespawnTime;
    public bool ShowStarCounter { get; init; } = true;
    public bool ShowStatusIcon { get; init; } = true;
    public bool ShowActionBar { get; init; } = true;
    public bool ShowFullInterface { get; init; } = true;
    public bool ShowEndDialog { get; init; } = true;
    public RewardBundleBase RewardBundle { get; init; } = RewardBundleBase.CreateEmpty();
    public RewardBundleBase MemberRewardBundle { get; init; } = RewardBundleBase.CreateEmpty();
    public RewardBundleBase PreviewRewardBundle { get; init; } = RewardBundleBase.CreateEmpty();
    public Dictionary<int, RewardBundleBase> ProfilePreviewRewardBundles { get; init; } = [];
    public List<MiniGameUiObjective> Objectives { get; init; } = [];
    public EncounterDataCommon EncounterDataCommon { get; init; } = new();
}
