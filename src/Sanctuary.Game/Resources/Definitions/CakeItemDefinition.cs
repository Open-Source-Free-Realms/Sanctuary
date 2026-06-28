namespace Sanctuary.Game.Resources.Definitions;

public class CakeItemDefinition
{
    public int ItemId { get; set; }
    public CakeItemType Type { get; set; }
    public int CooldownMs { get; set; }
}

public enum CakeItemType
{
    ScaredyCake,
    BossCake
}