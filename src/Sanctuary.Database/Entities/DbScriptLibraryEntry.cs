using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// A GM-saved script in the script library. Templates shipped with the server live in
/// Resources/ScriptTemplates instead and are never stored here.
/// </summary>
public class DbScriptLibraryEntry
{
    public string Name { get; set; } = null!;

    public string Source { get; set; } = null!;

    public string? Description { get; set; }

    public ulong? UpdatedBy { get; set; }

    public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
}
