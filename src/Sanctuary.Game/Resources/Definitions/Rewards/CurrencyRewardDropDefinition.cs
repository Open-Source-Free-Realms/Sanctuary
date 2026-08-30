namespace Sanctuary.Game.Resources.Definitions.Rewards;

public sealed class CurrencyRewardDropDefinition : RewardDropDefinition
{
    public CurrencyType CurrencyType { get; set; }
    public int Amount { get; set; }
}
