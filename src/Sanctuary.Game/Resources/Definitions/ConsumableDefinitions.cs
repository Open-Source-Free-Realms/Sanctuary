using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// Container for all consumable item definitions loaded from Consumables.json.
/// Includes Boomboxes, FoodEffects, and Transformations.
/// </summary>
public class ConsumableDefinitions
{
    /// <summary>
    /// Boombox items that play music and dances.
    /// </summary>
    public List<BoomboxDefinition> Boomboxes { get; set; } = new();

    /// <summary>
    /// Food items that apply visual effects to the player.
    /// </summary>
    public List<FoodEffectDefinition> FoodEffects { get; set; } = new();

    /// <summary>
    /// Transformation abilities that change the player's appearance.
    /// </summary>
    public List<TransformAbilityDefinition> Transformations { get; set; } = new();
}
