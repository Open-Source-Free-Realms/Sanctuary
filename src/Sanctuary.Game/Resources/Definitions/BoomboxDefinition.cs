namespace Sanctuary.Game.Resources.Definitions;

public class BoomboxDefinition
{
    public int ItemId { get; set; }
    public int ModelId { get; set; }
    public int[] EffectIds { get; set; } = [];
    public int[] DanceSequence { get; set; } = [];
    public int TransformModelId { get; set; }
}
