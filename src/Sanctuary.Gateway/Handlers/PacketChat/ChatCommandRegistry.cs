using System;
using System.Collections.Generic;
using System.Linq;

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

    public static void Initialize(IZoneManager zoneManager, IDbContextFactory<DatabaseContext> dbContextFactory, ILogger adminLogger)
    {
        _zoneManager = zoneManager;
        _dbContextFactory = dbContextFactory;
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
        var pools = connection.Player.Zone.GetCollectionNodePoolStatuses();
        var pool = args.Length == 1
            ? pools.FirstOrDefault(candidate => candidate.Key == args[0].ToLowerInvariant())
            : null;

        if (pool is null)
        {
            var available = string.Join(", ", pools.Select(candidate => candidate.Key));
            SendSystemMessage(connection, $"Unknown collection node pool. Available: {available}");
            return;
        }

        var heading = MathF.Atan2(connection.Player.Rotation.X, connection.Player.Rotation.Z);

        if (!connection.Player.Zone.TryPlaceCollectionNodeSpawn(
            pool.Key, connection.Player.Position, heading, out var spawn, out var activated))
        {
            SendSystemMessage(connection, "The collection node could not be saved.");
            return;
        }

        LogAction(connection, "Place collection node", $"{pool.Key} #{spawn.Id}");
        SendSystemMessage(connection, $"Saved {pool.Key} hard point #{spawn.Id}; " +
            (activated ? "activated now." : "inactive because the pool is at capacity."));
    }

    private static void ConfigureCollectionNodePool(GatewayConnection connection, string[] args)
    {
        var pools = connection.Player.Zone.GetCollectionNodePoolStatuses();
        var pool = args.Length > 0
            ? pools.FirstOrDefault(candidate => candidate.Key == args[0].ToLowerInvariant())
            : null;

        if (args.Length != 3 ||
            pool is null ||
            !int.TryParse(args[1], out var maxActiveNodes) || maxActiveNodes < 0 ||
            !int.TryParse(args[2], out var respawnSeconds) || respawnSeconds is < 1 or > 86400)
        {
            SendSystemMessage(connection,
                "Usage: !admin collection configure [pool] [maxActive: 0+] [respawnSeconds: 1-86400]");
            return;
        }

        if (!connection.Player.Zone.TryConfigureCollectionNodePool(
            pool.Key, maxActiveNodes, respawnSeconds, out var activeCount, out var target))
        {
            SendSystemMessage(connection, "The collection node pool could not be saved.");
            return;
        }

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

        if (!connection.Player.Zone.TryRemoveNearestCollectionNodeSpawn(
            connection.Player.Position, radius, out var removedSpawn))
        {
            SendSystemMessage(connection, $"No persistent collection node found within {radius:0.#} units.");
            return;
        }

        LogAction(connection, "Remove collection node", $"{removedSpawn.Pool} #{removedSpawn.Id}");
        SendSystemMessage(connection, $"Removed {removedSpawn.Pool} hard point #{removedSpawn.Id}.");
    }

    private static void RemoveCollectionNodeById(GatewayConnection connection, string idArgument)
    {
        if (!int.TryParse(idArgument.AsSpan(1), out var id))
        {
            SendSystemMessage(connection, $"Unknown collection node id {idArgument} in this zone.");
            return;
        }

        if (!connection.Player.Zone.TryRemoveCollectionNodeSpawn(id, out var removedSpawn))
        {
            SendSystemMessage(connection, "The collection node could not be removed from storage.");
            return;
        }

        LogAction(connection, "Remove collection node", $"{removedSpawn.Pool} #{id}");
        SendSystemMessage(connection, $"Removed {removedSpawn.Pool} hard point #{id}.");
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

            if (!connection.Player.Zone.GetCollectionNodePoolStatuses().Any(pool => pool.Key == poolFilter))
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

        var query = connection.Player.Zone.GetCollectionNodeSpawnStatuses(poolFilter);
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
            SendSystemMessage(connection, total == 0 ? "No persistent collection nodes." : $"No collection nodes on page {page}.");
            return;
        }

        var pageCount = (total + PageSize - 1) / PageSize;
        SendSystemMessage(connection, $"Collection nodes page {page}/{pageCount}:\n{string.Join("\n", entries)}");
    }

    private static void ListCollectionNodePools(GatewayConnection connection)
    {
        var entries = connection.Player.Zone.GetCollectionNodePoolStatuses()
            .Select(pool => $"{pool.Key}: {pool.ActiveCount}/{pool.TargetActiveCount} active, " +
                $"{pool.HardPointCount} points, {pool.RespawnSeconds}s, type {pool.NodeType}")
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