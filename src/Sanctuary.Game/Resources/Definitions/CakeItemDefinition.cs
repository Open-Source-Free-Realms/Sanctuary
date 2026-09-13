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
    public int[] SpawnEffectIds { get; set; } = [21];
    public int LifetimeMs { get; set; } = 60000;
    public int InteractCooldownMs { get; set; } = 2000;

    public int OneShotAnimation { get; set; }
    public int OneShotAnimationMs { get; set; } = 3000;
    public int OneShotIntervalMs { get; set; } = 10000;

    // ScaredyCake
    public int[][] ScareGroups { get; set; } = [];

    // BossCake
    public int[] TransformAbilityIds { get; set; } = [];
}

public enum CakeItemType
{
    ScaredyCake,
    BossCake
}
