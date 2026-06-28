using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public class ConsumableDefinitions
{
    public List<BoomboxDefinition> Boomboxes { get; set; } = new();

    public List<FoodEffectDefinition> FoodEffects { get; set; } = new();

    public List<TransformAbilityDefinition> Transformations { get; set; } = new();
}
