namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// A consumable that applies a random transformation from <see cref="TransformAbilityIds"/>
/// when used (e.g. the Jack-O-Lantern rolls one of the boss transformations).
/// </summary>
public class RandomTransformFoodDefinition
{
    public int ItemId { get; set; }
    public int[] TransformAbilityIds { get; set; } = [];
}
