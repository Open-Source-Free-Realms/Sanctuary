using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public enum MiniGameNpcButtonType
{
    Encounter = 2,
    MiniGame = 11
}

public class MiniGameNpcButtonDefinition
{
    public int Id { get; set; }
    public int IconId { get; set; }
    public int ButtonText { get; set; }
    public MiniGameNpcButtonType Type { get; set; }
    public int Param1 { get; set; }
}

public class MiniGameNpcDefinition
{
    public int NameId { get; set; }
    public string? Comment { get; set; }
    public List<MiniGameNpcButtonDefinition> Buttons { get; set; } = [];
}
