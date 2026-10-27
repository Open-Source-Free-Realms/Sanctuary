using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// A GM-made item: a copy of an existing item with some fields overridden. Registered as item
/// 9000000 + <see cref="Id"/> (see Sanctuary.Game.Authoring.CustomItemManager).
/// </summary>
public class DbCustomItem
{
    public int Id { get; set; }

    /// <summary>
    /// The existing item it copies (model, slot, stats, ...).
    /// </summary>
    public int BaseItemId { get; set; }

    /// <summary>
    /// GM-facing label; the client shows the item's NameId string.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// JSON object of overridden fields (see CustomItemManager.Fields).
    /// </summary>
    public string Overrides { get; set; } = "{}";

    public ulong? UpdatedBy { get; set; }

    public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
}
