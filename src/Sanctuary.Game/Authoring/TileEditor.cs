using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Authoring;

/// <summary>
/// One entry of a kit: a saved prop or NPC row, positioned relative to the kit's origin and facing.
/// </summary>
public sealed class TileKitEntry
{
    public DbWorldFixture? Fixture { get; set; }
    public DbWorldNpc? WorldNpc { get; set; }

    public float Dx { get; set; }
    public float Dy { get; set; }
    public float Dz { get; set; }

    /// <summary>
    /// Heading relative to the kit's facing, in radians.
    /// </summary>
    public float Heading { get; set; }
}

/// <summary>
/// An undoable tile edit; <see cref="Revert"/> runs in the zone the edit was made in.
/// </summary>
public sealed record TileUndo(string Label, IZone Zone, Action<WorldZone> Revert);

public sealed class TileEditorState
{
    public const int MaxUndo = 20;

    public List<ulong> Selection { get; } = [];
    public IZone? SelectionZone { get; set; }
    public List<TileUndo> Undo { get; } = [];

    public void PushUndo(TileUndo undo)
    {
        Undo.Add(undo);

        if (Undo.Count > MaxUndo)
            Undo.RemoveAt(0);
    }
}

public interface ITileEditor
{
    TileEditorState GetState(Player player);

    IReadOnlyList<DbWorldKit> ListKits();

    bool TryGetKit(string name, out List<TileKitEntry> entries);

    string? SaveKit(string name, List<TileKitEntry> entries, string? note, ulong? updatedBy);

    bool DeleteKit(string name);
}

/// <summary>
/// Per-GM selection and undo for the tile editor, plus saved kits (WorldKits table).
/// Selections and undo history are in memory only and end with the session.
/// </summary>
public sealed class TileEditor : ITileEditor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly ConcurrentDictionary<ulong, TileEditorState> _states = new();

    public TileEditor(IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public TileEditorState GetState(Player player) => _states.GetOrAdd(player.Guid, _ => new TileEditorState());

    public IReadOnlyList<DbWorldKit> ListKits()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.WorldKits.AsNoTracking().OrderBy(x => x.Name).ToList();
    }

    public bool TryGetKit(string name, out List<TileKitEntry> entries)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var kit = dbContext.WorldKits.AsNoTracking().SingleOrDefault(x => x.Name == name);

        entries = kit is null ? [] : JsonSerializer.Deserialize<List<TileKitEntry>>(kit.Entries, JsonOptions) ?? [];

        return kit is not null;
    }

    public string? SaveKit(string name, List<TileKitEntry> entries, string? note, ulong? updatedBy)
    {
        if (!ScriptTemplate.IsValidName(name))
            return "Kit names are 1-32 letters, digits, '_' or '-'.";

        using var dbContext = _dbContextFactory.CreateDbContext();

        var kit = dbContext.WorldKits.SingleOrDefault(x => x.Name == name);

        if (kit is null)
        {
            kit = new DbWorldKit { Name = name };
            dbContext.WorldKits.Add(kit);
        }

        kit.Entries = JsonSerializer.Serialize(entries, JsonOptions);
        kit.Note = note is { Length: > 200 } ? note[..200] : note;
        kit.UpdatedBy = updatedBy;
        kit.Updated = DateTimeOffset.UtcNow;

        dbContext.SaveChanges();

        return null;
    }

    public bool DeleteKit(string name)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.WorldKits.Where(x => x.Name == name).ExecuteDelete() > 0;
    }

    /// <summary>
    /// Rotates a ground offset by a heading change, matching QuaternionExtensions.FromHeading
    /// (heading 0 faces +Z, increasing towards +X).
    /// </summary>
    public static (float Dx, float Dz) Rotate(float dx, float dz, float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);

        return (dx * cos + dz * sin, -dx * sin + dz * cos);
    }

    public static Vector4 Centroid(IReadOnlyCollection<TileEntity> entities) => entities.Count == 0
        ? Vector4.Zero
        : new Vector4(entities.Average(x => x.Position.X), entities.Average(x => x.Position.Y), entities.Average(x => x.Position.Z), 1f);
}
