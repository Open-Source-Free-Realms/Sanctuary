using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

public class CollectionChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;
    private readonly IRewardManager _rewardManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public string KeyWord => "collection";

    public string Usage => "<pools|configure [pool] [maxActive] [respawnSeconds]|place [pool]|remove [radius|#id]|list [pool] [page]" +
        "|status [player]|grant <collectionId> [player]|reset <collectionId> [player]>";

    public string Description => "Manage collection nodes, and players' collections (status, grant every entry, reset).";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public CollectionChatCommand(IChatCommandManager chatCommandManager, IZoneManager zoneManager, IResourceManager resourceManager,
        IRewardManager rewardManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _chatCommandManager = chatCommandManager;
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
        _rewardManager = rewardManager;
        _dbContextFactory = dbContextFactory;
    }

    private void LogAction(Player invoker, string action, string? targetName = null, string? detail = null)
    {
        _chatCommandManager.LogAction(this, invoker, action, targetName, detail);
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length < 1)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "place":
                PlaceCollectionNode(invoker, args[1..]);
                break;
            case "pools":
                ListCollectionNodePools(invoker);
                break;
            case "configure":
                ConfigureCollectionNodePool(invoker, args[1..]);
                break;
            case "remove":
                RemoveCollectionNode(invoker, args[1..]);
                break;
            case "list":
                ListCollectionNodes(invoker, args[1..]);
                break;
            case "status":
                ShowPlayerCollections(invoker, args[1..]);
                break;
            case "grant":
                return GrantPlayerCollection(invoker, args[1..]);
            case "reset":
                return ResetPlayerCollection(invoker, args[1..]);
            default:
                return false;
        }

        return true;
    }

    private void PlaceCollectionNode(Player invoker, string[] args)
    {
        var pools = invoker.Zone.GetCollectionNodePoolStatuses();
        var pool = args.Length == 1
            ? pools.FirstOrDefault(candidate => candidate.Key == args[0].ToLowerInvariant())
            : null;

        if (pool is null)
        {
            var available = string.Join(", ", pools.Select(candidate => candidate.Key));
            ChatHelper.SendSystemMessage(invoker, $"Unknown collection node pool. Available: {available}");
            return;
        }

        var heading = MathF.Atan2(invoker.Rotation.X, invoker.Rotation.Z);

        if (!invoker.Zone.TryPlaceCollectionNodeSpawn(
            pool.Key, invoker.Position, heading, out var spawn, out var activated))
        {
            ChatHelper.SendSystemMessage(invoker, "The collection node could not be saved.");
            return;
        }

        LogAction(invoker, "Place collection node", $"{pool.Key} #{spawn.Id}");
        ChatHelper.SendSystemMessage(invoker, $"Saved {pool.Key} hard point #{spawn.Id}; " +
            (activated ? "activated now." : "inactive because the pool is at capacity."));
    }

    private void ConfigureCollectionNodePool(Player invoker, string[] args)
    {
        var pools = invoker.Zone.GetCollectionNodePoolStatuses();
        var pool = args.Length > 0
            ? pools.FirstOrDefault(candidate => candidate.Key == args[0].ToLowerInvariant())
            : null;

        if (args.Length != 3 ||
            pool is null ||
            !int.TryParse(args[1], out var maxActiveNodes) || maxActiveNodes < 0 ||
            !int.TryParse(args[2], out var respawnSeconds) || respawnSeconds is < 1 or > 86400)
        {
            ChatHelper.SendSystemMessage(invoker,
                "Usage: !admin collection configure [pool] [maxActive: 0+] [respawnSeconds: 1-86400]");
            return;
        }

        if (!invoker.Zone.TryConfigureCollectionNodePool(
            pool.Key, maxActiveNodes, respawnSeconds, out var activeCount, out var target))
        {
            ChatHelper.SendSystemMessage(invoker, "The collection node pool could not be saved.");
            return;
        }

        LogAction(invoker, "Configure collection node pool", pool.Key,
            $"maxActive={maxActiveNodes}, respawnSeconds={respawnSeconds}");
        ChatHelper.SendSystemMessage(invoker,
            $"Configured {pool.Key}: {activeCount}/{target} active, respawn {respawnSeconds}s.");
    }

    private void RemoveCollectionNode(Player invoker, string[] args)
    {
        if (args.Length > 1)
        {
            ChatHelper.SendSystemMessage(invoker, "Usage: !admin collection remove [radius|#id]");
            return;
        }

        if (args.Length == 1 && args[0].StartsWith('#'))
        {
            RemoveCollectionNodeById(invoker, args[0]);
            return;
        }

        var radius = 10f;

        if (args.Length > 0 && (!float.TryParse(args[0], out radius) || radius <= 0 || radius > 100))
        {
            ChatHelper.SendSystemMessage(invoker, "Removal radius must be between 0 and 100.");
            return;
        }

        if (!invoker.Zone.TryRemoveNearestCollectionNodeSpawn(
            invoker.Position, radius, out var removedSpawn))
        {
            ChatHelper.SendSystemMessage(invoker, $"No persistent collection node found within {radius:0.#} units.");
            return;
        }

        LogAction(invoker, "Remove collection node", $"{removedSpawn.Pool} #{removedSpawn.Id}");
        ChatHelper.SendSystemMessage(invoker, $"Removed {removedSpawn.Pool} hard point #{removedSpawn.Id}.");
    }

    private void RemoveCollectionNodeById(Player invoker, string idArgument)
    {
        if (!int.TryParse(idArgument.AsSpan(1), out var id))
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown collection node id {idArgument} in this zone.");
            return;
        }

        if (!invoker.Zone.TryRemoveCollectionNodeSpawn(id, out var removedSpawn))
        {
            ChatHelper.SendSystemMessage(invoker, "The collection node could not be removed from storage.");
            return;
        }

        LogAction(invoker, "Remove collection node", $"{removedSpawn.Pool} #{id}");
        ChatHelper.SendSystemMessage(invoker, $"Removed {removedSpawn.Pool} hard point #{id}.");
    }

    private static void ListCollectionNodes(Player invoker, string[] args)
    {
        const int PageSize = 10;
        string? poolFilter = null;
        var page = 1;

        if (args.Length > 2)
        {
            ChatHelper.SendSystemMessage(invoker, "Usage: !admin collection list [pool] [page]");
            return;
        }

        if (args.Length > 0 && int.TryParse(args[0], out page))
        {
            if (args.Length > 1)
            {
                ChatHelper.SendSystemMessage(invoker, "Usage: !admin collection list [pool] [page]");
                return;
            }
        }
        else if (args.Length > 0)
        {
            poolFilter = args[0].ToLowerInvariant();

            if (!invoker.Zone.GetCollectionNodePoolStatuses().Any(pool => pool.Key == poolFilter))
            {
                ChatHelper.SendSystemMessage(invoker, $"Unknown collection node pool {poolFilter}.");
                return;
            }

            page = 1;

            if (args.Length > 1 && !int.TryParse(args[1], out page))
            {
                ChatHelper.SendSystemMessage(invoker, "Usage: !admin collection list [pool] [page]");
                return;
            }
        }

        if (page < 1)
        {
            ChatHelper.SendSystemMessage(invoker, "Page must be a positive number.");
            return;
        }

        var query = invoker.Zone.GetCollectionNodeSpawnStatuses(poolFilter);
        var total = query.Count;
        var entries = query
            .OrderBy(spawn => spawn.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(spawn => $"#{spawn.Id} {spawn.Pool} {(spawn.Active ? "active" : "inactive")} " +
                $"({spawn.Position.X:0.0}, {spawn.Position.Y:0.0}, {spawn.Position.Z:0.0})")
            .ToArray();

        if (entries.Length == 0)
        {
            ChatHelper.SendSystemMessage(invoker, total == 0 ? "No persistent collection nodes." : $"No collection nodes on page {page}.");
            return;
        }

        var pageCount = (total + PageSize - 1) / PageSize;
        ChatHelper.SendSystemMessage(invoker, $"Collection nodes page {page}/{pageCount}:\n{string.Join("\n", entries)}");
    }

    private static void ListCollectionNodePools(Player invoker)
    {
        var entries = invoker.Zone.GetCollectionNodePoolStatuses()
            .Select(pool => $"{pool.Key}: {pool.ActiveCount}/{pool.TargetActiveCount} active, " +
                $"{pool.HardPointCount} points, {pool.RespawnSeconds}s, type {pool.NodeType}")
            .ToArray();

        ChatHelper.SendSystemMessage(invoker, entries.Length == 0
            ? "No collection node pools are configured for this zone."
            : string.Join("\n", entries));
    }

    #region Player collections

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

    private static string GetCollectionName(CollectionDefinition collection) =>
        GameNameHelper.Get(collection.NameId, $"Collection {collection.Id}");

    private static (HashSet<int> OwnedItems, HashSet<int> CollectedEntries) GetProgress(Player player) =>
        (player.Items.Select(item => item.Definition).ToHashSet(), player.CollectedCollectionEntryIds.ToHashSet());

    private void ShowPlayerCollections(Player invoker, string[] args)
    {
        if (!TryGetTarget(invoker, args, out var target, out _))
            return;

        var (ownedItems, collectedEntries) = GetProgress(target);

        var started = _resourceManager.Collections.Values
            .Where(collection => collection.IsStarted(ownedItems, collectedEntries))
            .OrderBy(collection => collection.Id)
            .ToList();

        ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName} has started {started.Count} collection(s).");

        foreach (var collection in started)
        {
            var collected = collection.Entries.Count(entry => entry.ItemDefinitionId > 0
                ? ownedItems.Contains(entry.ItemDefinitionId)
                : collectedEntries.Contains(entry.Id));

            ChatHelper.SendSystemMessage(invoker, $"  {collection.Id} {GetCollectionName(collection)}: {collected}/{collection.Entries.Count}");
        }
    }

    private bool TryGetCollection(Player invoker, string[] args, out CollectionDefinition collection)
    {
        collection = null!;

        if (!int.TryParse(args[0], out var collectionId))
            return false;

        if (!_resourceManager.Collections.TryGetValue(collectionId, out collection!))
        {
            ChatHelper.SendSystemMessage(invoker, $"Collection {collectionId} does not exist.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Fills in every missing entry: item entries are granted as items, the rest are saved as collected.
    /// </summary>
    private bool GrantPlayerCollection(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (!TryGetCollection(invoker, args, out var collection) || !TryGetTarget(invoker, args[1..], out var target, out var targetName))
            return true;

        var (ownedItems, collectedEntries) = GetProgress(target);
        var characterId = GuidHelper.GetPlayerId(target.Guid);
        var granted = 0;

        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            foreach (var entry in collection.Entries)
            {
                if (entry.ItemDefinitionId > 0)
                {
                    if (!ownedItems.Contains(entry.ItemDefinitionId) && _rewardManager.TryGrantItem(target, entry.ItemDefinitionId, 0))
                        granted++;

                    continue;
                }

                if (collectedEntries.Contains(entry.Id))
                    continue;

                dbContext.CollectionEntries.Add(new DbCollectionEntry
                {
                    CharacterId = characterId,
                    CollectionId = collection.Id,
                    EntryId = entry.Id
                });

                target.CollectedCollectionEntryIds.TryAdd(entry.Id);
                granted++;
            }

            dbContext.SaveChanges();
        }

        SendCollection(target, collection);

        LogAction(invoker, "Grant collection", targetName, collection.Id.ToString());
        ChatHelper.SendSystemMessage(invoker, $"Granted {granted} missing entr{(granted == 1 ? "y" : "ies")} of {GetCollectionName(collection)}.");
        return true;
    }

    /// <summary>
    /// Clears the collected (non-item) entries of a collection. Items stay in the inventory.
    /// </summary>
    private bool ResetPlayerCollection(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (!TryGetCollection(invoker, args, out var collection) || !TryGetTarget(invoker, args[1..], out var target, out var targetName))
            return true;

        var characterId = GuidHelper.GetPlayerId(target.Guid);

        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            var entries = dbContext.CollectionEntries
                .Where(entry => entry.CharacterId == characterId && entry.CollectionId == collection.Id)
                .ToList();

            dbContext.CollectionEntries.RemoveRange(entries);
            dbContext.SaveChanges();
        }

        foreach (var entry in collection.Entries.Where(entry => entry.ItemDefinitionId <= 0))
            target.CollectedCollectionEntryIds.TryRemove(entry.Id);

        SendCollection(target, collection);

        LogAction(invoker, "Reset collection", targetName, collection.Id.ToString());
        ChatHelper.SendSystemMessage(invoker, $"Reset {GetCollectionName(collection)}. Item entries stay collected while the player still owns the items.");
        return true;
    }

    /// <summary>
    /// Replaces the client's copy of the collection with the player's current progress.
    /// </summary>
    private static void SendCollection(Player player, CollectionDefinition collection)
    {
        var (ownedItems, collectedEntries) = GetProgress(player);

        using var writer = new PacketWriter();
        collection.CreateClientCollection(player.Guid, ownedItems, collectedEntries).Serialize(writer);

        player.SendTunneled(new ClientUpdatePacketCollectionRemove { CollectionId = collection.Id });
        player.SendTunneled(new ClientUpdatePacketCollectionStart { Payload = writer.Buffer });
    }

    #endregion
}
