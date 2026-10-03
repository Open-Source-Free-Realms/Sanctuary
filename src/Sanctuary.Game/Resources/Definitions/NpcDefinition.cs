using System;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Sanctuary.Game.Resources.Definitions;

public class NpcDefinition
{
    public int Id { get; set; }

    public int NameId { get; set; }
    public string? Name { get; set; }

    public int ModelId { get; set; }
    public string ModelFileName { get; set; } = null!;

    public string? TextureAlias { get; set; }
    public bool HideNamePlate { get; set; }
    public int Animation { get; set; } = 1;
    public float VerticalOffset { get; set; }
    public float? Scale { get; set; }
    public int BoomboxItemId { get; set; }
    public string[]? Scripts { get; set; }
}
