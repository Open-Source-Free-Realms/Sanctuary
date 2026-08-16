using System.Text.Json.Serialization;

using Sanctuary.Core.Collections;

namespace Sanctuary.Game.Resources.Definitions.Rewards;

// NOTE: I wonder if this is foolproof...
// TODO: Implement the other reward types.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "RewardType")]
[JsonDerivedType(typeof(ItemRewardDropDefinition), "Item")]
[JsonDerivedType(typeof(CurrencyRewardDropDefinition), "Currency")]
public abstract class RewardDropDefinition : IWeighted
{
    public int Weight { get; set; } = 1;
}
