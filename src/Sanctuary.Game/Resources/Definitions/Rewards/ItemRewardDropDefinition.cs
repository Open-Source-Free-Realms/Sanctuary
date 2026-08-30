using System.Collections.Generic;

using Sanctuary.Core.Collections;

namespace Sanctuary.Game.Resources.Definitions.Rewards;

public sealed class ItemRewardDropDefinition : RewardDropDefinition
{
    public int ItemDefinitionId { get; set; }
    public List<TintDropDefinition> Tints { get; set; } = [];

    public int Quantity { get; set; } = 1;

    private WeightedDropTable<TintDropDefinition>? _tintTable;
    public WeightedDropTable<TintDropDefinition>? TintTable => Tints.Count == 0
        ? null
        : _tintTable ??= new WeightedDropTable<TintDropDefinition>(Tints);
}
