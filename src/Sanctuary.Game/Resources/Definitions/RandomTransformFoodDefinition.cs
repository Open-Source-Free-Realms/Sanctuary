namespace Sanctuary.Game.Resources.Definitions;
/// A consumable that applies a random transformation from <see cref="TransformAbilityIds"/> when used.
public class RandomTransformFoodDefinition
{
    public int ItemId { get; set; }
    public int[] TransformAbilityIds { get; set; } = [];
    public string? Comment { get; set; }
}
