using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Authoring;

public interface ICustomItemManager
{
    /// <summary>
    /// Registers every saved custom item. Call after resources load.
    /// </summary>
    void Load();

    IReadOnlyList<DbCustomItem> GetAll(string? search = null);

    bool TryGet(int id, out DbCustomItem item);

    DbCustomItem? Create(int baseItemId, string label, ulong? createdBy, out string? error);

    /// <summary>
    /// Sets the label, or overrides a field ("clear" removes the override). Returns the error, or null.
    /// </summary>
    string? Set(int id, string field, string value, ulong? updatedBy);

    bool TryDelete(int id, out string? error);

    /// <summary>
    /// Each supported field with its current value and whether it's overridden.
    /// </summary>
    IReadOnlyList<(string Field, string Value, bool Overridden)> DescribeFields(DbCustomItem item);
}

/// <summary>
/// GM-made items: copies of existing items with some fields (model, texture, tint, icon, rarity, ...)
/// overridden. Each is registered in the item definitions as <see cref="ItemIdBase"/> + its row id,
/// so it's sent to clients like any other item.
/// </summary>
public class CustomItemManager : ICustomItemManager
{
    public const int ItemIdBase = 9000000;

    /// <summary>
    /// Overridable fields. Names, descriptions and icons are client ids: the client only shows its own strings and art.
    /// </summary>
    public static readonly string[] Fields =
        ["name", "desc", "model", "texture", "tint", "icon", "icontint", "rarity", "cost", "stack", "effect", "members", "notrade", "nosale"];

    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly object _lock = new();

    public CustomItemManager(ILogger<CustomItemManager> logger, IServiceProvider serviceProvider, IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
    }

    public static bool IsCustomItemId(int itemDefinitionId) => itemDefinitionId > ItemIdBase;

    public void Load()
    {
        var registered = 0;

        foreach (var item in GetAll())
        {
            if (TryBuild(item, out var definition, out var error))
            {
                _resourceManager.ClientItemDefinitions[definition.Id] = definition;
                registered++;
            }
            else
            {
                _logger.LogWarning("Custom item {Id} was not registered: {Error}", item.Id, error);
            }
        }

        _logger.LogInformation("Registered {Count} custom item(s).", registered);
    }

    public IReadOnlyList<DbCustomItem> GetAll(string? search = null)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var items = dbContext.CustomItems.AsNoTracking().OrderBy(x => x.Id).ToList();

        return string.IsNullOrEmpty(search)
            ? items
            : [.. items.Where(x => x.Label.Contains(search, StringComparison.OrdinalIgnoreCase) || (ItemIdBase + x.Id).ToString() == search)];
    }

    public bool TryGet(int id, out DbCustomItem item)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        item = dbContext.CustomItems.AsNoTracking().SingleOrDefault(x => x.Id == id)!;

        return item is not null;
    }

    public DbCustomItem? Create(int baseItemId, string label, ulong? createdBy, out string? error)
    {
        error = null;

        if (IsCustomItemId(baseItemId) || !_resourceManager.ClientItemDefinitions.ContainsKey(baseItemId))
        {
            error = $"Unknown base item {baseItemId}; use a regular (non-custom) item id.";
            return null;
        }

        var item = new DbCustomItem
        {
            BaseItemId = baseItemId,
            Label = string.IsNullOrWhiteSpace(label) ? $"Copy of {baseItemId}" : Truncate(label, 64),
            UpdatedBy = createdBy
        };

        lock (_lock)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.CustomItems.Add(item);
            dbContext.SaveChanges();

            Register(item);
        }

        return item;
    }

    public string? Set(int id, string field, string value, ulong? updatedBy)
    {
        lock (_lock)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var item = dbContext.CustomItems.SingleOrDefault(x => x.Id == id);

            if (item is null)
                return $"No custom item {id}.";

            if (field == "label")
            {
                if (value.Length is 0 or > 64)
                    return "Labels are 1-64 characters.";

                item.Label = value;
            }
            else
            {
                if (!Fields.Contains(field))
                    return $"Unknown field '{field}'. Fields: label, {string.Join(", ", Fields)}.";

                var overrides = ReadOverrides(item);

                if (value.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    overrides.Remove(field);
                }
                else
                {
                    if (Validate(field, value) is { } error)
                        return error;

                    overrides[field] = value;
                }

                item.Overrides = JsonSerializer.Serialize(overrides, JsonOptions);
            }

            item.UpdatedBy = updatedBy;
            item.Updated = DateTimeOffset.UtcNow;
            dbContext.SaveChanges();

            Register(item);

            return null;
        }
    }

    public bool TryDelete(int id, out string? error)
    {
        error = null;

        lock (_lock)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var itemDefinitionId = ItemIdBase + id;

            // Owned copies would point at an item the client no longer knows.
            var owners = dbContext.Items.Count(x => x.Definition == itemDefinitionId);

            if (owners > 0)
            {
                error = $"{owners} inventory stack(s) still hold item {itemDefinitionId}; it can't be deleted.";
                return false;
            }

            if (dbContext.CustomItems.Where(x => x.Id == id).ExecuteDelete() == 0)
            {
                error = $"No custom item {id}.";
                return false;
            }

            _resourceManager.ClientItemDefinitions.Remove(itemDefinitionId);

            return true;
        }
    }

    public IReadOnlyList<(string Field, string Value, bool Overridden)> DescribeFields(DbCustomItem item)
    {
        var overrides = ReadOverrides(item);

        _resourceManager.ClientItemDefinitions.TryGetValue(ItemIdBase + item.Id, out var definition);

        return [.. Fields.Select(field => (field, definition is null ? string.Empty : Read(definition, field), overrides.ContainsKey(field)))];
    }

    private void Register(DbCustomItem item)
    {
        if (!TryBuild(item, out var definition, out var error))
        {
            _logger.LogWarning("Custom item {Id} could not be built: {Error}", item.Id, error);
            return;
        }

        _resourceManager.ClientItemDefinitions[definition.Id] = definition;

        // Clients cache definitions, so push the new one to everyone online.
        using var writer = new PacketWriter();

        writer.Write(new List<ClientItemDefinition> { definition });

        var packet = new PlayerUpdatePacketItemDefinitions { Payload = writer.Buffer };

        foreach (var player in _serviceProvider.GetRequiredService<IZoneManager>().Zones.SelectMany(zone => zone.Players))
            player.SendTunneled(packet);
    }

    private bool TryBuild(DbCustomItem item, out ClientItemDefinition definition, out string? error)
    {
        definition = null!;
        error = null;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(item.BaseItemId, out var baseDefinition))
        {
            error = $"Base item {item.BaseItemId} is missing.";
            return false;
        }

        definition = baseDefinition.Clone();
        definition.Id = ItemIdBase + item.Id;
        definition.Comment = item.Label;

        // The clone shares the base item's icon; give it its own before changing it.
        definition.Icon = new IconData { Id = baseDefinition.Icon.Id, TintId = baseDefinition.Icon.TintId };

        foreach (var (field, value) in ReadOverrides(item))
            Apply(definition, field, value);

        return true;
    }

    private static Dictionary<string, string> ReadOverrides(DbCustomItem item)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(item.Overrides, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Validate(string field, string value) => field switch
    {
        "model" or "texture" or "tint" => value.Length is 0 or > 128 ? $"{field} must be 1-128 characters." : null,
        "members" or "notrade" or "nosale" => value is "on" or "off" ? null : $"{field} is on or off.",
        "stack" => int.TryParse(value, out var stack) && stack >= 1 ? null : "stack must be at least 1.",
        _ => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= 0 ? null : $"{field} must be a whole number of 0 or more."
    };

    private static void Apply(ClientItemDefinition definition, string field, string value)
    {
        int Number() => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;

        switch (field)
        {
            case "name": definition.NameId = Number(); break;
            case "desc": definition.DescriptionId = Number(); break;
            case "model": definition.ModelName = value; break;
            case "texture": definition.TextureAlias = value; break;
            case "tint": definition.TintAlias = value; break;
            case "icon": definition.Icon.Id = Number(); break;
            case "icontint": definition.Icon.TintId = Number(); break;
            case "rarity": definition.Rarity = Number(); break;
            case "cost": definition.Cost = Number(); break;
            case "stack": definition.MaxStackSize = Number(); break;
            case "effect": definition.CompositeEffectId = Number(); break;
            case "members": definition.MembersOnly = value == "on"; break;
            case "notrade": definition.NoTrade = value == "on"; break;
            case "nosale": definition.NoSale = value == "on"; break;
        }
    }

    private static string Read(ClientItemDefinition definition, string field) => field switch
    {
        "name" => definition.NameId.ToString(CultureInfo.InvariantCulture),
        "desc" => definition.DescriptionId.ToString(CultureInfo.InvariantCulture),
        "model" => definition.ModelName ?? string.Empty,
        "texture" => definition.TextureAlias ?? string.Empty,
        "tint" => definition.TintAlias ?? string.Empty,
        "icon" => definition.Icon.Id.ToString(CultureInfo.InvariantCulture),
        "icontint" => definition.Icon.TintId.ToString(CultureInfo.InvariantCulture),
        "rarity" => definition.Rarity.ToString(CultureInfo.InvariantCulture),
        "cost" => definition.Cost.ToString(CultureInfo.InvariantCulture),
        "stack" => definition.MaxStackSize.ToString(CultureInfo.InvariantCulture),
        "effect" => definition.CompositeEffectId.ToString(CultureInfo.InvariantCulture),
        "members" => definition.MembersOnly ? "on" : "off",
        "notrade" => definition.NoTrade ? "on" : "off",
        "nosale" => definition.NoSale ? "on" : "off",
        _ => string.Empty
    };

    private static string Truncate(string text, int length) => text.Length > length ? text[..length] : text;
}
