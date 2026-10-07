using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public class ZoneAreaDefinition
{
    public int Id { get; set; }

    /// <summary>
    /// Circle - XZ 1 and Radius
    /// Rectangle - XZ 1 and 2
    /// Polygon - Points containing XZ pairs
    /// </summary>
    public string Shape { get; set; } = null!;

    public float X1 { get; set; }
    public float Z1 { get; set; }

    public float X2 { get; set; }
    public float Z2 { get; set; }

    public float Radius { get; set; }

    public List<float[]> Points { get; set; } = [];
}