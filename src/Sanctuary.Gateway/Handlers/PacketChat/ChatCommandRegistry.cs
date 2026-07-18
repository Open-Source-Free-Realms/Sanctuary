using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Gateway.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Handlers;

public enum ChatCommandRole
{
    Player = 0,
    Mod = 1,
    Admin = 2
}

public delegate void ChatCommandHandler(GatewayConnection connection, string[] args);

public sealed record ChatCommandDefinition(ChatCommandRole RequiredRole, string Usage, ChatCommandHandler Handler);

public static class ChatCommandRegistry
{
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static IResourceManager _resourceManager = null!;
    private static ILogger _adminLogger = null!;

    private static readonly Dictionary<string, ChatCommandDefinition> Commands = new Dictionary<string, ChatCommandDefinition>
    {
        ["ban"] = new ChatCommandDefinition(ChatCommandRole.Mod, "!admin ban [player] [minutes]", Ban),
        ["unban"] = new ChatCommandDefinition(ChatCommandRole.Mod, "!admin unban [player]", Unban),
        ["mute"] = new ChatCommandDefinition(ChatCommandRole.Mod, "!admin mute [player] [minutes]", Mute),
        ["unmute"] = new ChatCommandDefinition(ChatCommandRole.Mod, "!admin unmute [player]", Unmute),
        ["promote"] = new ChatCommandDefinition(ChatCommandRole.Admin, "!admin promote [player]", Promote),
        ["demote"] = new ChatCommandDefinition(ChatCommandRole.Admin, "!admin demote [player]", Demote),
        ["help"] = new ChatCommandDefinition(ChatCommandRole.Mod, "!admin help", Help),
        ["collection"] = new ChatCommandDefinition(ChatCommandRole.Admin,
            "!admin collection <pools|configure [pool] [maxActive] [respawnSeconds]|place [pool]|remove [radius|#id]|list [pool] [page]>", Collection),
    };

    public static void Initialize(IZoneManager zoneManager, IDbContextFactory<DatabaseContext> dbContextFactory,
        IResourceManager resourceManager, ILogger adminLogger)
    {
        _zoneManager = zoneManager;
        _dbContextFactory = dbContextFactory;
        _resourceManager = resourceManager;
        _adminLogger = adminLogger;
    }

    public static ChatCommandRole GetPlayerRole(Player player)
    {
        return GetRoleFromFlags(player.IsAdmin, player.IsMod);
    }

    private static ChatCommandRole GetRoleFromFlags(bool isAdmin, bool isMod)
    {
        if (isAdmin)
            return ChatCommandRole.Admin;

        if (isMod)
            return ChatCommandRole.Mod;

        return ChatCommandRole.Player;
    }

    private static bool TryParseTarget(string[] args, out string parsedTargetName, out DateTimeOffset? parsedUntilValue, out string? error)
    {
        parsedTargetName = string.Empty;
        parsedUntilValue = null;
        error = null;

        if (args.Length == 0)
            return false;

        if (args.Length > 1 && int.TryParse(args[^1], out var minutes))
        {
            if (minutes <= 0)
            {
                error = "Duration must be a positive number of minutes.";
                return false;
            }

            parsedTargetName = string.Join(' ', args[..^1]);
            parsedUntilValue = DateTimeOffset.UtcNow.AddMinutes(minutes);
        }
        else
        {
            parsedTargetName = string.Join(' ', args);
        }

        return true;
    }

    private static bool IsSelfTarget(GatewayConnection connection, string targetName)
    {
        return connection.Player.Name.FullName == targetName;
    }

    private static bool IsAuthorizedAgainstTarget(ChatCommandRole playerRole, ChatCommandRole targetRole)
    {
        return playerRole > targetRole;
    }

    private static bool TryResolveTarget(GatewayConnection connection, DatabaseContext dbContext, string targetName, out ulong targetUserId)
    {
        var target = dbContext.Characters
            .Where(character => character.FullName == targetName)
            .Select(character => new { character.UserId, character.User.IsAdmin, character.User.IsMod })
            .SingleOrDefault();

        if (target is null)
        {
            SendSystemMessage(connection, $"No player named \"{targetName}\" was found.");
            targetUserId = 0;
            return false;
        }

        ChatCommandRole playerRole = GetRoleFromFlags(connection.Player.IsAdmin, connection.Player.IsMod);
        ChatCommandRole targetRole = GetRoleFromFlags(target.IsAdmin, target.IsMod);
        if (!IsAuthorizedAgainstTarget(playerRole, targetRole))
        {
            SendSystemMessage(connection, "You don't have permission to target this player.");
            targetUserId = 0;
            return false;
        }

        targetUserId = target.UserId;
        return true;
    }

    public static void HandleCommand(GatewayConnection connection, string message)
    {
        string[] tokens = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length < 2)
        {
            SendSystemMessage(connection, $"Invalid command format. Type !admin help for a list of commands.");
            return;
        }

        string name = tokens[1];
        string[] args = tokens[2..];

        if (!Commands.TryGetValue(name, out var command))
        {
            SendSystemMessage(connection, $"Unknown command: {message}. Type !admin help for a list of commands.");
            return;
        }

        ChatCommandRole playerRole = GetPlayerRole(connection.Player);
        if (playerRole < command.RequiredRole)
        {
            SendSystemMessage(connection, "You don't have permission to use this command.");
            return;
        }

        command.Handler(connection, args);
    }

    private static void Ban(GatewayConnection connection, string[] args)
    {
        if (!TryParseTarget(args, out var targetName, out var banUntilTime, out var error))
        {
            SendSystemMessage(connection, error ?? $"Usage: {Commands["ban"].Usage}");
            return;
        }

        if (IsSelfTarget(connection, targetName))
        {
            SendSystemMessage(connection, "You cannot ban yourself.");
            return;
        }

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        if (!TryResolveTarget(connection, dbContext, targetName, out var targetUserId))
            return;

        DateTimeOffset lockedUntil = banUntilTime ?? DateTimeOffset.MaxValue;
        dbContext.Users
            .Where(user => user.Id == targetUserId)
            .ExecuteUpdate(user => user
                .SetProperty(u => u.LockedUntil, lockedUntil));

        if (_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
            targetPlayer.Disconnect();

        LogAction(connection, "Ban", targetName, banUntilTime is null ? "Permanent" : $"Until: {banUntilTime:u}");

        SendSystemMessage(connection, banUntilTime is null
            ? $"{targetName} has been banned permanently."
            : $"{targetName} has been banned until {banUntilTime:u}.");
    }

    private static void Unban(GatewayConnection connection, string[] args)
    {
        if (args.Length < 1)
        {
            SendSystemMessage(connection, $"Usage: {Commands["unban"].Usage}");
            return;
        }

        string targetName = string.Join(' ', args);

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        if (!TryResolveTarget(connection, dbContext, targetName, out var targetUserId))
            return;

        dbContext.Users
            .Where(user => user.Id == targetUserId)
            .ExecuteUpdate(user => user
                .SetProperty(u => u.LockedUntil, (DateTimeOffset?)null));

        LogAction(connection, "Unban", targetName);

        SendSystemMessage(connection, $"{targetName} has been unbanned.");
    }

    private static void Mute(GatewayConnection connection, string[] args)
    {
        if (!TryParseTarget(args, out var targetName, out var muteUntilTime, out var error))
        {
            SendSystemMessage(connection, error ?? $"Usage: {Commands["mute"].Usage}");
            return;
        }

        if (muteUntilTime == null)
        {
            SendSystemMessage(connection, $"Please specify a duration in minutes for mute. Usage: {Commands["mute"].Usage}");
            return;
        }

        if (IsSelfTarget(connection, targetName))
        {
            SendSystemMessage(connection, "You cannot mute yourself.");
            return;
        }

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        if (!TryResolveTarget(connection, dbContext, targetName, out var targetUserId))
            return;

        dbContext.Users
            .Where(user => user.Id == targetUserId)
            .ExecuteUpdate(user => user
                .SetProperty(u => u.MutedUntil, muteUntilTime));

        if (_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
        {
            targetPlayer.MutedUntil = muteUntilTime;
        }

        LogAction(connection, "Mute", targetName, $"Until: {muteUntilTime:u}");

        SendSystemMessage(connection, $"{targetName} has been muted until {muteUntilTime:u}.");
    }

    private static void Unmute(GatewayConnection connection, string[] args)
    {
        if (args.Length < 1)
        {
            SendSystemMessage(connection, $"Usage: {Commands["unmute"].Usage}");
            return;
        }

        string targetName = string.Join(' ', args);

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        if (!TryResolveTarget(connection, dbContext, targetName, out var targetUserId))
            return;

        dbContext.Users
            .Where(user => user.Id == targetUserId)
            .ExecuteUpdate(user => user
                .SetProperty(u => u.MutedUntil, (DateTimeOffset?)null));

        if (_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
        {
            targetPlayer.MutedUntil = null;
        }

        LogAction(connection, "Unmute", targetName);

        SendSystemMessage(connection, $"{targetName} has been unmuted.");
    }

    private static void SetMod(GatewayConnection connection, string targetName, bool isMod)
    {
        if (GetPlayerRole(connection.Player) < ChatCommandRole.Admin)
        {
            SendSystemMessage(connection, "You don't have permission to use this command.");
            return;
        }

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        var target = dbContext.Characters.SingleOrDefault(character => character.FullName == targetName);

        if (target is null)
        {
            SendSystemMessage(connection, $"No player named \"{targetName}\" was found.");
            return;
        }

        dbContext.Users
            .Where(user => user.Id == target.UserId)
            .ExecuteUpdate(user => user.SetProperty(u => u.IsMod, isMod));

        if (_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
            targetPlayer.IsMod = isMod;

        LogAction(connection, isMod ? "Promote" : "Demote", targetName);

        SendSystemMessage(connection, isMod
            ? $"{targetName} has been promoted to moderator."
            : $"{targetName} has been demoted from moderator.");
    }

    private static void Promote(GatewayConnection connection, string[] args)
    {
        if (args.Length < 1)
        {
            SendSystemMessage(connection, $"Usage: {Commands["promote"].Usage}");
            return;
        }

        string parsedTargetName = string.Join(' ', args);
        SetMod(connection, parsedTargetName, true);
    }

    private static void Demote(GatewayConnection connection, string[] args)
    {
        if (args.Length < 1)
        {
            SendSystemMessage(connection, $"Usage: {Commands["demote"].Usage}");
            return;
        }

        SetMod(connection, string.Join(' ', args), false);
    }

    private static void Help(GatewayConnection connection, string[] args)
    {
        ChatCommandRole role = GetPlayerRole(connection.Player);

        string[] usages = Commands.Values
            .Where(command => role >= command.RequiredRole)
            .OrderBy(command => command.Usage)
            .Select(command => command.Usage)
            .ToArray();

        string fullHelpString = "";
        foreach (var usage in usages)
        {
            fullHelpString += usage + "\n";
        }
        SendSystemMessage(connection, fullHelpString);
    }

    private static void Collection(GatewayConnection connection, string[] args)
    {
        if (args.Length == 0)
        {
            SendSystemMessage(connection, $"Usage: {Commands["collection"].Usage}");
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "place":
                PlaceCollectionNode(connection, args[1..]);
                break;
            case "pools":
                ListCollectionNodePools(connection);
                break;
            case "configure":
                ConfigureCollectionNodePool(connection, args[1..]);
                break;
            case "remove":
                RemoveCollectionNode(connection, args[1..]);
                break;
            case "list":
                ListCollectionNodes(connection, args[1..]);
                break;
            default:
                SendSystemMessage(connection, $"Usage: {Commands["collection"].Usage}");
                break;
        }
    }

    private static void PlaceCollectionNode(GatewayConnection connection, string[] args)
    {
        if (args.Length != 1 ||
            !_resourceManager.CollectionNodePools.TryGetValue(args[0].ToLowerInvariant(), out var pool) ||
            pool.ZoneDefinitionId != connection.Player.Zone.DefinitionId ||
            !_resourceManager.CollectionNodeTypes.TryGetValue(pool.NodeType, out var type))
        {
            var available = string.Join(", ", _resourceManager.CollectionNodePools.Values
                .Where(candidate => candidate.ZoneDefinitionId == connection.Player.Zone.DefinitionId)
                .Select(candidate => candidate.Key)
                .Order());
            SendSystemMessage(connection, $"Unknown collection node pool. Available: {available}");
            return;
        }

        var position = connection.Player.Position;
        position.Y += type.PlacementYOffset;
        var heading = MathF.Atan2(connection.Player.Rotation.X, connection.Player.Rotation.Z);

        if (!_resourceManager.CollectionNodeSpawns.TryAddPersistent(pool.Key, position, heading, out var spawn))
        {
            SendSystemMessage(connection, "The collection node could not be saved.");
            return;
        }

        var activated = connection.Player.Zone.TryActivateCollectionNodeSpawn(spawn, out _);

        LogAction(connection, "Place collection node", $"{pool.Key} #{spawn.Id}");
        SendSystemMessage(connection, $"Saved {pool.Key} hard point #{spawn.Id}; " +
            (activated ? "activated now." : "inactive because the pool is at capacity."));
    }

    private static void ConfigureCollectionNodePool(GatewayConnection connection, string[] args)
    {
        if (args.Length != 3 ||
            !_resourceManager.CollectionNodePools.TryGetValue(args[0].ToLowerInvariant(), out var pool) ||
            pool.ZoneDefinitionId != connection.Player.Zone.DefinitionId ||
            !int.TryParse(args[1], out var maxActiveNodes) || maxActiveNodes < 0 ||
            !int.TryParse(args[2], out var respawnSeconds) || respawnSeconds is < 1 or > 86400)
        {
            SendSystemMessage(connection,
                "Usage: !admin collection configure [pool] [maxActive: 0+] [respawnSeconds: 1-86400]");
            return;
        }

        if (!_resourceManager.CollectionNodePools.TryUpdatePersistent(pool.Key, maxActiveNodes, respawnSeconds))
        {
            SendSystemMessage(connection, "The collection node pool could not be saved.");
            return;
        }

        var activeCount = connection.Player.Zone.ReconcileCollectionNodePool(pool.Key);
        var hardPointCount = _resourceManager.CollectionNodeSpawns.Values.Count(spawn => spawn.Pool == pool.Key);
        var target = pool.GetTargetActiveCount(hardPointCount);

        LogAction(connection, "Configure collection node pool", pool.Key,
            $"maxActive={maxActiveNodes}, respawnSeconds={respawnSeconds}");
        SendSystemMessage(connection,
            $"Configured {pool.Key}: {activeCount}/{target} active, respawn {respawnSeconds}s.");
    }

    private static void RemoveCollectionNode(GatewayConnection connection, string[] args)
    {
        if (args.Length > 1)
        {
            SendSystemMessage(connection, "Usage: !admin collection remove [radius|#id]");
            return;
        }

        if (args.Length == 1 && args[0].StartsWith('#'))
        {
            RemoveCollectionNodeById(connection, args[0]);
            return;
        }

        var radius = 10f;

        if (args.Length > 0 && (!float.TryParse(args[0], out radius) || radius <= 0 || radius > 100))
        {
            SendSystemMessage(connection, "Removal radius must be between 0 and 100.");
            return;
        }

        var playerPosition = connection.Player.Position;
        var node = connection.Player.Zone.Npcs
            .OfType<CollectionNode>()
            .Where(candidate => _resourceManager.CollectionNodeSpawns.ContainsKey(candidate.SpawnDefinition.Id))
            .Select(candidate => new
            {
                Node = candidate,
                DistanceSquared = Vector3.DistanceSquared(
                    new Vector3(candidate.Position.X, candidate.Position.Y, candidate.Position.Z),
                    new Vector3(playerPosition.X, playerPosition.Y, playerPosition.Z))
            })
            .Where(candidate => candidate.DistanceSquared <= radius * radius)
            .OrderBy(candidate => candidate.DistanceSquared)
            .Select(candidate => candidate.Node)
            .FirstOrDefault();

        if (node is null)
        {
            SendSystemMessage(connection, $"No persistent collection node found within {radius:0.#} units.");
            return;
        }

        if (!_resourceManager.CollectionNodeSpawns.TryRemovePersistent(node.SpawnDefinition.Id))
        {
            SendSystemMessage(connection, "The collection node could not be removed from storage.");
            return;
        }

        node.Dispose();
        connection.Player.Zone.ReconcileCollectionNodePool(node.PoolDefinition.Key);
        LogAction(connection, "Remove collection node", $"{node.PoolDefinition.Key} #{node.SpawnDefinition.Id}");
        SendSystemMessage(connection, $"Removed {node.PoolDefinition.Key} hard point #{node.SpawnDefinition.Id}.");
    }

    private static void RemoveCollectionNodeById(GatewayConnection connection, string idArgument)
    {
        if (!int.TryParse(idArgument.AsSpan(1), out var id) ||
            !_resourceManager.CollectionNodeSpawns.TryGetValue(id, out var spawn) ||
            !_resourceManager.CollectionNodePools.TryGetValue(spawn.Pool, out var pool) ||
            pool.ZoneDefinitionId != connection.Player.Zone.DefinitionId)
        {
            SendSystemMessage(connection, $"Unknown collection node id {idArgument} in this zone.");
            return;
        }

        if (!_resourceManager.CollectionNodeSpawns.TryRemovePersistent(id))
        {
            SendSystemMessage(connection, "The collection node could not be removed from storage.");
            return;
        }

        var activeNode = connection.Player.Zone.Npcs
            .OfType<CollectionNode>()
            .FirstOrDefault(node => node.SpawnDefinition.Id == id);

        activeNode?.Dispose();
        connection.Player.Zone.ReconcileCollectionNodePool(spawn.Pool);
        LogAction(connection, "Remove collection node", $"{spawn.Pool} #{id}");
        SendSystemMessage(connection, $"Removed {spawn.Pool} hard point #{id}.");
    }

    private static void ListCollectionNodes(GatewayConnection connection, string[] args)
    {
        const int PageSize = 10;
        string? poolFilter = null;
        var page = 1;

        if (args.Length > 2)
        {
            SendSystemMessage(connection, "Usage: !admin collection list [pool] [page]");
            return;
        }

        if (args.Length > 0 && int.TryParse(args[0], out page))
        {
            if (args.Length > 1)
            {
                SendSystemMessage(connection, "Usage: !admin collection list [pool] [page]");
                return;
            }
        }
        else if (args.Length > 0)
        {
            poolFilter = args[0].ToLowerInvariant();

            if (!_resourceManager.CollectionNodePools.TryGetValue(poolFilter, out var filteredPool) ||
                filteredPool.ZoneDefinitionId != connection.Player.Zone.DefinitionId)
            {
                SendSystemMessage(connection, $"Unknown collection node pool {poolFilter}.");
                return;
            }

            page = 1;

            if (args.Length > 1 && !int.TryParse(args[1], out page))
            {
                SendSystemMessage(connection, "Usage: !admin collection list [pool] [page]");
                return;
            }
        }

        if (page < 1)
        {
            SendSystemMessage(connection, "Page must be a positive number.");
            return;
        }

        var activeIds = connection.Player.Zone.Npcs
            .OfType<CollectionNode>()
            .Select(node => node.SpawnDefinition.Id)
            .ToHashSet();
        var zonePoolKeys = _resourceManager.CollectionNodePools.Values
            .Where(pool => pool.ZoneDefinitionId == connection.Player.Zone.DefinitionId)
            .Select(pool => pool.Key)
            .ToHashSet();
        var query = _resourceManager.CollectionNodeSpawns.Values
            .Where(spawn => zonePoolKeys.Contains(spawn.Pool) &&
                (poolFilter is null || spawn.Pool == poolFilter))
            .OrderBy(spawn => spawn.Id)
            .ToArray();
        var total = query.Length;
        var entries = query
            .OrderBy(spawn => spawn.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(spawn => $"#{spawn.Id} {spawn.Pool} {(activeIds.Contains(spawn.Id) ? "active" : "inactive")} " +
                $"({spawn.Position[0]:0.0}, {spawn.Position[1]:0.0}, {spawn.Position[2]:0.0})")
            .ToArray();

        if (entries.Length == 0)
        {
            SendSystemMessage(connection, total == 0 ? "No persistent collection nodes." : $"No collection nodes on page {page}.");
            return;
        }

        var pageCount = (total + PageSize - 1) / PageSize;
        SendSystemMessage(connection, $"Collection nodes page {page}/{pageCount}:\n{string.Join("\n", entries)}");
    }

    private static void ListCollectionNodePools(GatewayConnection connection)
    {
        var entries = _resourceManager.CollectionNodePools.Values
            .Where(pool => pool.ZoneDefinitionId == connection.Player.Zone.DefinitionId)
            .OrderBy(pool => pool.Key)
            .Select(pool =>
            {
                var hardPointCount = _resourceManager.CollectionNodeSpawns.Values.Count(spawn => spawn.Pool == pool.Key);
                var activeCount = connection.Player.Zone.Npcs.OfType<CollectionNode>()
                    .Count(node => node.PoolDefinition.Key == pool.Key);
                var target = pool.GetTargetActiveCount(hardPointCount);

                return $"{pool.Key}: {activeCount}/{target} active, {hardPointCount} points, " +
                    $"{pool.RespawnSeconds}s, type {pool.NodeType}";
            })
            .ToArray();

        SendSystemMessage(connection, entries.Length == 0
            ? "No collection node pools are configured for this zone."
            : string.Join("\n", entries));
    }

    private static void SendSystemMessage(GatewayConnection connection, string message)
    {
        ChatHelper.SendSystemMessage(connection, message);
    }

    private static void LogAction(GatewayConnection connection, string action, string targetName, string? detail = null)
    {
        _adminLogger.LogInformation("{Action}|Actor: \"{ActorName}\" ({ActorGuid}), Target: \"{TargetName}\"{Detail}",
            action,
            connection.Player.Name,
            connection.Player.Guid,
            targetName,
            detail is null ? string.Empty : $", {detail}"
        );
    }
}
