using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Database.Entities;

namespace Sanctuary.Game.Authoring;

public interface IScriptLibrary
{
    /// <summary>
    /// Templates first, then GM-saved entries, each sorted by name. Search matches name or description.
    /// </summary>
    IReadOnlyList<ScriptTemplate> GetAll(string? search = null);

    bool TryGet(string name, out ScriptTemplate entry);

    /// <summary>
    /// Saves a GM entry. Templates can't be overwritten.
    /// </summary>
    bool TrySave(string name, string source, string? description, ulong? updatedBy, out string? error);

    bool TryDelete(string name, out string? error);
}

/// <summary>
/// Reusable scripts for NPCs and props. Shipped templates are read from Resources/ScriptTemplates at
/// startup and are read-only; GM entries live in the ScriptLibrary table.
/// </summary>
public class ScriptLibrary : IScriptLibrary
{
    public static readonly string TemplatesDirectory = Path.Combine(ResourceManager.BaseDirectory, "ScriptTemplates");

    private readonly ILogger _logger;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly Lazy<Dictionary<string, ScriptTemplate>> _templates;
    private readonly Lazy<ConcurrentDictionary<string, ScriptTemplate>> _entries;

    public ScriptLibrary(ILogger<ScriptLibrary> logger, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;

        _templates = new(LoadTemplates);
        _entries = new(LoadEntries);
    }

    private Dictionary<string, ScriptTemplate> LoadTemplates()
    {
        var templates = new Dictionary<string, ScriptTemplate>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(TemplatesDirectory))
            return templates;

        foreach (var file in Directory.GetFiles(TemplatesDirectory, "*.lua"))
        {
            var template = ScriptTemplate.Parse(File.ReadAllText(file), null, null, true);

            var name = string.IsNullOrEmpty(template.Name) ? Path.GetFileNameWithoutExtension(file) : template.Name;

            if (!ScriptTemplate.IsValidName(name) || !templates.TryAdd(name, template with { Name = name }))
                _logger.LogWarning("Skipped script template {File}: invalid or duplicate name {Name}.", file, name);
        }

        _logger.LogInformation("Loaded {Count} script templates.", templates.Count);

        return templates;
    }

    private ConcurrentDictionary<string, ScriptTemplate> LoadEntries()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return new(dbContext.ScriptLibrary
            .AsNoTracking()
            .ToList()
            .Select(x => ScriptTemplate.Parse(x.Source, x.Name, x.Description, false))
            .ToDictionary(x => x.Name), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ScriptTemplate> GetAll(string? search = null)
    {
        bool Matches(ScriptTemplate entry) => string.IsNullOrEmpty(search)
            || entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Description.Contains(search, StringComparison.OrdinalIgnoreCase);

        return
        [
            .. _templates.Value.Values.Where(Matches).OrderBy(x => x.Name),
            .. _entries.Value.Values.Where(Matches).OrderBy(x => x.Name)
        ];
    }

    public bool TryGet(string name, out ScriptTemplate entry)
    {
        if (_templates.Value.TryGetValue(name, out entry!))
            return true;

        return _entries.Value.TryGetValue(name, out entry!);
    }

    public bool TrySave(string name, string source, string? description, ulong? updatedBy, out string? error)
    {
        error = null;

        if (!ScriptTemplate.IsValidName(name))
        {
            error = "Names are 1-32 letters, digits, '_' or '-'.";
            return false;
        }

        if (_templates.Value.ContainsKey(name))
        {
            error = $"{name} is a shipped template and can't be overwritten; save under another name.";
            return false;
        }

        if (description is { Length: > 200 })
            description = description[..200];

        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            var dbEntry = dbContext.ScriptLibrary.SingleOrDefault(x => x.Name == name);

            if (dbEntry is null)
            {
                dbEntry = new DbScriptLibraryEntry { Name = name, Source = source };
                dbContext.ScriptLibrary.Add(dbEntry);
            }

            dbEntry.Source = source;
            dbEntry.Description = description;
            dbEntry.UpdatedBy = updatedBy;
            dbEntry.Updated = DateTimeOffset.UtcNow;

            dbContext.SaveChanges();
        }

        _entries.Value[name] = ScriptTemplate.Parse(source, name, description, false);

        return true;
    }

    public bool TryDelete(string name, out string? error)
    {
        error = null;

        if (_templates.Value.ContainsKey(name))
        {
            error = $"{name} is a shipped template and can't be deleted.";
            return false;
        }

        if (!_entries.Value.TryRemove(name, out _))
        {
            error = $"No library script named {name}.";
            return false;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        dbContext.ScriptLibrary.Where(x => x.Name == name).ExecuteDelete();

        return true;
    }
}
