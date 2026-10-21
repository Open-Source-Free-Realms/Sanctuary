using Sanctuary.Packet.Common;

namespace Sanctuary.Game.MiniGames;

public sealed class MiniGameUiObjective
{
    public int Id { get; init; }
    public int NameId { get; init; }
    public int DescriptionId { get; init; }
    public int Total { get; init; }
    public int CompleteValue { get; init; }
    public bool IsFailCondition { get; init; }
    public bool ShowTask { get; init; }
    public RewardBundleInfo RewardBundle { get; init; } = new();
}
