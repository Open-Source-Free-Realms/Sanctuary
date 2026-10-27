using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Referee pet tools. Giving a pet takes the same path as buying one.
/// </summary>
public class PetChatCommand : IChatCommand
{
    private const int FindLimit = 15;

    private readonly IChatCommandManager _chatCommandManager;
    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public string KeyWord => "pet";

    public string Usage => "list [player] | find <text> | give <itemId> [tint] [player]";

    public string Description => "Referee pet tools: list a player's pets, find pet items, and give a pet.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public PetChatCommand(IChatCommandManager chatCommandManager, IZoneManager zoneManager, IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _chatCommandManager = chatCommandManager;
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        return args[0].ToLowerInvariant() switch
        {
            "list" => HandleList(invoker, args[1..]),
            "find" => HandleFind(invoker, args[1..]),
            "give" => HandleGive(invoker, args[1..]),
            _ => false
        };
    }

    private bool TryGetTarget(Player invoker, string[] args, out Player target, out string? targetName)
    {
        targetName = args.Length > 0 ? string.Join(' ', args) : null;
        target = invoker;

        if (targetName is not null && !_zoneManager.TryGetPlayer(targetName, out target))
        {
            ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
            return false;
        }

        return true;
    }

    private string GetItemName(int itemDefinitionId) =>
        _resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var item)
            ? GameNameHelper.Get(item.NameId) ?? item.Comment ?? $"Item {itemDefinitionId}"
            : $"Item {itemDefinitionId}";

    private bool HandleList(Player invoker, string[] args)
    {
        if (!TryGetTarget(invoker, args, out var target, out _))
            return true;

        ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName} has {target.PetDefinitions.Count} pet(s){(target.Pet is null ? "" : ", one summoned")}.");

        foreach (var (petId, itemDefinitionId) in target.PetDefinitions.OrderBy(x => x.Key))
            ChatHelper.SendSystemMessage(invoker, $"  #{petId} {GetItemName(itemDefinitionId)} (item {itemDefinitionId})");

        return true;
    }

    private bool HandleFind(Player invoker, string[] args)
    {
        var search = string.Join(' ', args).Trim();

        if (search.Length == 0)
            return false;

        var matches = _resourceManager.ClientItemDefinitions.Values
            .Where(item => item.Type == ItemDefinitionHelper.PetItemType)
            .Select(item => (Item: item, Name: GetItemName(item.Id)))
            .Where(x => x.Item.Id.ToString() == search || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ChatHelper.SendSystemMessage(invoker, $"{matches.Count} pet(s) match \"{search}\".");

        foreach (var (item, name) in matches.Take(FindLimit))
            ChatHelper.SendSystemMessage(invoker, $"  {item.Id} {name}");

        return true;
    }

    private bool HandleGive(Player invoker, string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var itemDefinitionId))
            return false;

        var rest = args[1..];
        int? tint = null;

        if (rest.Length > 0 && int.TryParse(rest[0], out var parsedTint))
        {
            tint = parsedTint;
            rest = rest[1..];
        }

        if (!TryGetTarget(invoker, rest, out var target, out var targetName))
            return true;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var clientItemDefinition)
            || !PetHelper.TryGetModel(_resourceManager, clientItemDefinition, out var modelDefinition))
        {
            ChatHelper.SendSystemMessage(invoker, $"Item {itemDefinitionId} is not a pet with a model.");
            return true;
        }

        var characterId = GuidHelper.GetPlayerId(target.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var lastPetId = dbContext.Pets
            .Where(pet => pet.CharacterId == characterId)
            .Select(pet => (int?)pet.Id)
            .Max() ?? 0;

        var dbPet = PetHelper.CreatePet(lastPetId + 1, clientItemDefinition.Id, tint ?? clientItemDefinition.Icon.TintId);
        dbPet.CharacterId = characterId;

        dbContext.Pets.Add(dbPet);

        if (dbContext.SaveChanges() <= 0)
        {
            ChatHelper.SendSystemMessage(invoker, "Failed to save the pet.");
            return true;
        }

        PetHelper.AddPet(target, _resourceManager, dbPet, clientItemDefinition, modelDefinition);
        target.PlayerPet.SendPetList();

        _chatCommandManager.LogAction(this, invoker, "Give pet", targetName, itemDefinitionId.ToString());
        ChatHelper.SendSystemMessage(invoker, $"Gave {GetItemName(itemDefinitionId)} to {target.Name.FullName}.");
        return true;
    }
}
