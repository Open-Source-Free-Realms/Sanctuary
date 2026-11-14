using System.Collections.Generic;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.MiniGames;

public sealed class MiniGameResult
{
    public List<MiniGameScoreInfo> Scores { get; init; } = [];

    public int EarnedStars { get; init; }

    public int TimeRemaining { get; init; }

    public RewardBundleBase RewardBundle { get; init; } = new();

    public RewardBundleBase MemberRewardBundle { get; init; } = new();

    public RewardBundleBase? LootWheelRewardBundle { get; init; }
}
