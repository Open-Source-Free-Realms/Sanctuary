using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class CollectionChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "collection";

    public string Usage => "<pools|configure [pool] [maxActive] [respawnSeconds]|place [pool]|remove [radius|#id]|list [pool] [page]>";

    public string Description => "Manage collection nodes.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public CollectionChatCommand(IChatCommandManager chatCommandManager)
    {
        _chatCommandManager = chatCommandManager;
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
}