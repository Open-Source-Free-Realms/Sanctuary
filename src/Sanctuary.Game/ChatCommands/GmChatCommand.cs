using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Backs the referee panel in the patched chat window (ClientMods/chatWindowCtrl).
/// Lookups reply with <see cref="ChatHelper.SendGmPanelData"/> lines the panel renders.
/// </summary>
public class GmChatCommand : IChatCommand
{
    private const int PageSize = 12;

    /// <summary>
    /// English names by NameId, extracted from the client locale (ClientMods/extract_names.py).
    /// </summary>
    private static readonly string NamesFile = Path.Combine(ResourceManager.BaseDirectory, "Names.json");

    private readonly ILogger _logger;
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;

    private readonly Lazy<Dictionary<int, string>> _names;

    public string KeyWord => "gm";
    public string Usage => "[open] | items|npcs|houses|players <page> [search] | goto <player> | summon <player> | pos";
    public string Description => "Referee tools: opens the GM panel and answers its lookups.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public GmChatCommand(ILogger<GmChatCommand> logger, IChatCommandManager chatCommandManager, IZoneManager zoneManager,
        IResourceManager resourceManager)
    {
        _logger = logger;
        _chatCommandManager = chatCommandManager;
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;

        _names = new(LoadNames);
    }

    private Dictionary<int, string> LoadNames()
    {
        try
        {
            if (File.Exists(NamesFile))
            {
                using var stream = File.OpenRead(NamesFile);

                var names = JsonSerializer.Deserialize<Dictionary<int, string>>(stream);

                if (names is not null)
                    return names;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load \"{file}\".", NamesFile);
        }

        _logger.LogWarning("No GM lookup names loaded, falling back to data file comments.");
        return [];
    }

    private string? GetName(int nameId) => _names.Value.TryGetValue(nameId, out var name) ? name : null;

    public bool Handle(Player invoker, string[] args)
    {
        var subCommand = args.Length == 0 ? "open" : args[0].ToLowerInvariant();

        switch (subCommand)
        {
            case "open":
                ChatHelper.SendGmPanelData(invoker, "open", invoker.ChatCommandRole);
                return true;

            case "items":
                return HandleItems(invoker, args[1..]);

            case "npcs":
                return HandleNpcs(invoker, args[1..]);

            case "houses":
                return HandleHouses(invoker, args[1..]);

            case "players":
                return HandlePlayers(invoker, args[1..]);

            case "goto":
                return HandleGoto(invoker, args[1..]);

            case "summon":
                return HandleSummon(invoker, args[1..]);

            case "pos":
                ChatHelper.SendGmPanelData(invoker, "pos", invoker.Zone.Name,
                    invoker.Position.X.ToString("0.0"), invoker.Position.Y.ToString("0.0"), invoker.Position.Z.ToString("0.0"));
                return true;

            default:
                return false;
        }
    }

    private static void ParsePage(string[] args, out int page, out string search)
    {
        page = 1;

        if (args.Length > 0 && int.TryParse(args[0], out var parsedPage))
        {
            page = Math.Max(1, parsedPage);
            args = args[1..];
        }

        search = string.Join(' ', args).Trim();
    }

    private static bool Matches(string search, int id, params string?[] values)
    {
        if (search.Length == 0 || id.ToString() == search)
            return true;

        return values.Any(value => value?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static void SendPage<T>(Player invoker, string kind, string search, int page, List<T> results, Action<T> sendRow)
    {
        var pages = Math.Max(1, (results.Count + PageSize - 1) / PageSize);
        page = Math.Min(page, pages);

        // Header first so the panel can clear the previous results.
        ChatHelper.SendGmPanelData(invoker, kind, search, page, pages, results.Count);

        foreach (var result in results.Skip((page - 1) * PageSize).Take(PageSize))
            sendRow(result);

        ChatHelper.SendGmPanelData(invoker, "end", kind);
    }

    private bool HandleItems(Player invoker, string[] args)
    {
        ParsePage(args, out var page, out var search);

        // Unnamed items are internal (test/unused), keep them out of the browser.
        var results = _resourceManager.ClientItemDefinitions.Values
            .Select(item => (Item: item, Name: GetName(item.NameId) ?? item.Comment))
            .Where(x => x.Name is not null && Matches(search, x.Item.Id, x.Name, x.Item.ModelName))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Item.Id)
            .ToList();

        SendPage(invoker, "items", search, page, results, x =>
            ChatHelper.SendGmPanelData(invoker, "item", x.Item.Id, x.Item.Icon.Id, x.Item.Icon.TintId, x.Item.Type, x.Name!));

        return true;
    }

    private bool HandleNpcs(Player invoker, string[] args)
    {
        ParsePage(args, out var page, out var search);

        var results = _resourceManager.Npcs.Values
            .Select(npc => (Npc: npc, Name: GetName(npc.NameId) ?? npc.Name ?? Path.GetFileNameWithoutExtension(npc.ModelFileName)))
            .Where(x => Matches(search, x.Npc.Id, x.Name, x.Npc.ModelFileName))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Npc.Id)
            .ToList();

        SendPage(invoker, "npcs", search, page, results, x =>
            ChatHelper.SendGmPanelData(invoker, "npc", x.Npc.Id, x.Npc.ModelId, x.Name));

        return true;
    }

    private bool HandleHouses(Player invoker, string[] args)
    {
        ParsePage(args, out var page, out var search);

        var results = _resourceManager.Houses.Values
            .Select(house => (House: house, Name: GetName(house.NameId) ?? $"House {house.Id}"))
            .Where(x => Matches(search, x.House.Id, x.Name))
            .OrderBy(x => x.House.Id)
            .ToList();

        SendPage(invoker, "houses", search, page, results, x =>
            ChatHelper.SendGmPanelData(invoker, "house", x.House.Id, GetHouseImageSetId(x.House), x.Name));

        return true;
    }

    /// <summary>
    /// The panel draws image sets; a house's own icon isn't one, so use its marketplace item's.
    /// </summary>
    private int GetHouseImageSetId(HouseDefinition house)
    {
        foreach (var itemDefinitionId in house.ItemDefinitionIds)
        {
            if (_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition) && itemDefinition.Icon.Id > 0)
                return itemDefinition.Icon.Id;
        }

        return 0;
    }

    private bool HandlePlayers(Player invoker, string[] args)
    {
        ParsePage(args, out var page, out var search);

        var results = _zoneManager.Zones
            .SelectMany(zone => zone.Players)
            .Where(player => search.Length == 0 || player.Name.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(player => player.Name.FullName)
            .ToList();

        SendPage(invoker, "players", search, page, results, player =>
            ChatHelper.SendGmPanelData(invoker, "player", player.Guid, player.Name.FullName, player.Zone.Name));

        return true;
    }

    private bool TryFindPlayer(Player invoker, string[] args, out Player target)
    {
        target = null!;

        var name = string.Join(' ', args).Trim();

        if (name.Length == 0)
            return false;

        if (!_zoneManager.TryGetPlayer(name, out var found))
        {
            ChatHelper.SendSystemMessage(invoker, $"Player \"{name}\" is not online.");
            return false;
        }

        target = found;
        return true;
    }

    private bool HandleGoto(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (!TryFindPlayer(invoker, args, out var target))
            return true;

        MovePlayer(invoker, target.Zone, target.Position, target.Rotation);

        _chatCommandManager.LogAction(this, invoker, "Goto", target.Name.FullName, null);
        ChatHelper.SendSystemMessage(invoker, $"Teleported to {target.Name.FullName}.");
        return true;
    }

    private bool HandleSummon(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (!TryFindPlayer(invoker, args, out var target))
            return true;

        MovePlayer(target, invoker.Zone, invoker.Position, invoker.Rotation);

        _chatCommandManager.LogAction(this, invoker, "Summon", target.Name.FullName, null);
        ChatHelper.SendSystemMessage(invoker, $"Summoned {target.Name.FullName}.");
        ChatHelper.SendSystemMessage(target, $"You were summoned by {invoker.Name.FullName}.");
        return true;
    }

    private static void MovePlayer(Player player, IZone zone, Vector4 position, Quaternion rotation)
    {
        if (player.Zone != zone)
        {
            player.TeleportToZone(zone.DefinitionId, zone.OwnerId, position, rotation);
            return;
        }

        player.UpdatePosition(position, rotation);

        player.SendTunneled(new ClientUpdatePacketUpdateLocation
        {
            Position = position,
            Rotation = rotation,
            Teleport = true
        });
    }
}
