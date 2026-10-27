using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// A saved group of props and NPCs (the tile editor's kits), placed again anywhere by a GM.
/// </summary>
public class DbWorldKit
{
    public string Name { get; set; } = null!;

    /// <summary>
    /// JSON list of entries, each a saved row with its offset and heading relative to the kit's origin.
    /// </summary>
    public string Entries { get; set; } = "[]";

    public string? Note { get; set; }

    public ulong? UpdatedBy { get; set; }

    public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
}
