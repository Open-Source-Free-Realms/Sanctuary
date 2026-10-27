using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// An NPC spawned into a world zone by a GM, including the edits made to it since.
/// </summary>
public class DbWorldNpc
{
    public ulong Id { get; set; }

    public int Zone { get; set; }

    /// <summary>
    /// Npcs.json definition id the NPC was spawned from.
    /// </summary>
    public int Definition { get; set; }

    public string? Name { get; set; }
    public int NameId { get; set; }

    public int ModelId { get; set; }
    public float Scale { get; set; }

    /// <summary>
    /// Looping animation the NPC idles with.
    /// </summary>
    public int Animation { get; set; }

    public int Disposition { get; set; }
    public bool HideNamePlate { get; set; }

    public int Behavior { get; set; }
    public float WanderRadius { get; set; }
    public float Speed { get; set; }

    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }

    public float RotationX { get; set; }
    public float RotationY { get; set; }
    public float RotationZ { get; set; }
    public float RotationW { get; set; }

    /// <summary>
    /// When set, the NPC only spawns while the <see cref="DbWorldEvent"/> with this key is active.
    /// </summary>
    public string? EventKey { get; set; }

    /// <summary>
    /// Whether players collide with it (off lets them walk through).
    /// </summary>
    public bool Collidable { get; set; } = true;

    /// <summary>
    /// GM-authored Lua (see Sanctuary.Scripting.ScriptSandbox).
    /// </summary>
    public string? Script { get; set; }

    /// <summary>
    /// GM-authored conversation tree, as JSON (see Sanctuary.Game.Conversations).
    /// </summary>
    public string? Conversation { get; set; }

    public ulong? CreatedBy { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
}
