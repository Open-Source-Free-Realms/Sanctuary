using System.Collections.Generic;

using Sanctuary.Core.Collections;

namespace Sanctuary.Game.Resources.Definitions.Rewards;

public sealed class RewardTableDefinition
{
    public string Key { get; set; } = string.Empty;
    public List<RewardDropDefinition> DropTable { get; set; } = [];

    private WeightedDropTable<RewardDropDefinition>? _table;
    public WeightedDropTable<RewardDropDefinition> Table => _table ??= new WeightedDropTable<RewardDropDefinition>(DropTable);
}
