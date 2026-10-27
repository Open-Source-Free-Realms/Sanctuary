using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// A prop placed into a world zone by a GM.
/// </summary>
public class DbWorldFixture
{
    public ulong Id { get; set; }

    public int Zone { get; set; }

    public int Tint { get; set; }
    public int Definition { get; set; }

    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }

    public float RotationX { get; set; }
    public float RotationY { get; set; }
    public float RotationZ { get; set; }
    public float RotationW { get; set; }

    public float Scale { get; set; }

    /// <summary>
    /// Whether players collide with it (off lets them walk through).
    /// </summary>
    public bool Collidable { get; set; } = true;

    /// <summary>
    /// GM-authored Lua (see Sanctuary.Scripting.ScriptSandbox).
    /// </summary>
    public string? Script { get; set; }

    /// <summary>
    /// Extra appearance set by GMs (texture, tint, effect, overhead icon, ...), as JSON.
    /// </summary>
    public string? Properties { get; set; }

    public ulong? CreatedBy { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
}