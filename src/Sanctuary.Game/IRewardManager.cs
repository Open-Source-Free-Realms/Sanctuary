using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game;

public interface IRewardManager
{
    bool TryRollReward(string rewardTableKey, out RewardDropDefinition? drop);

    bool TryGrantReward(Player player, RewardDropDefinition drop, ulong sourceGuid = 0);

    bool TryGrantItem(Player player, int itemDefinitionId, int tint, ulong sourceGuid = 0);

    bool TryGrantCurrency(Player player, CurrencyType currencyType, int amount);
}
