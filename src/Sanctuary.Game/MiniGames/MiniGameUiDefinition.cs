using System.Collections.Generic;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.MiniGames;

public sealed class MiniGameUiDefinition
{
    private const int CombatEncounterDetailsType = 1;

    public int GameId { get; init; }
    public int EncounterDefinitionId { get; init; }
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
    public int DurationMs { get; init; }
    public int RespawnTime { get; init; }
    public bool ShowStarCounter { get; init; }
    public bool ShowStatusIcon { get; init; }
    public bool ShowActionBar { get; init; }
    public bool StartupInCombat { get; init; }
    public bool ShowEndDialog { get; init; }
    public RewardBundleInfo RewardBundle { get; init; } = new();
    public RewardBundleInfo MemberRewardBundle { get; init; } = new();
    public RewardBundleInfo PreviewRewardBundle { get; init; } = new();
    public Dictionary<int, RewardBundleInfo> ProfilePreviewRewardBundles { get; init; } = [];
    public List<MiniGameUiObjective> Objectives { get; init; } = [];
    public EncounterDataCommon EncounterDataCommon { get; init; } = new();
}
