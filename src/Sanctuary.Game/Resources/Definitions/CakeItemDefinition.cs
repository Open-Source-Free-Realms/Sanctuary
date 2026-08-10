namespace Sanctuary.Game.Resources.Definitions;

public class CakeItemDefinition
{
    public int ItemId { get; set; }
    public CakeItemType Type { get; set; }
    public int CooldownMs { get; set; }
    public int ModelId { get; set; }
    public int NameId { get; set; }
    public int CursorId { get; set; } = 5;
    public int Animation { get; set; } = 1;
    public int SpawnPoofEffectId { get; set; } = 21;
    public int LifetimeMs { get; set; } = 60000;

    // ScaredyCake
    public int[][] ScareGroups { get; set; } = [];
    public int ScareCooldownMs { get; set; } = 2000;

    // BossCake
    public int[] TransformAbilityIds { get; set; } = [];
}

public enum CakeItemType
{
    ScaredyCake,
    BossCake
}
