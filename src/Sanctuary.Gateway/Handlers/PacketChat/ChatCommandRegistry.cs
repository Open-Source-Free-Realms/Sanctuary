using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
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
        ["ban"] = new ChatCommandDefinition(ChatCommandRole.Mod, "!admin ban <player> [minutes]", Ban),
        ["unban"] = new ChatCommandDefinition(ChatCommandRole.Mod, "!admin unban <player>", Unban),
        ["mute"] = new ChatCommandDefinition(ChatCommandRole.Mod, "!admin mute <player> [minutes]", Mute),
        ["unmute"] = new ChatCommandDefinition(ChatCommandRole.Mod, "!admin unmute <player>", Unmute),
        ["kick"] = new ChatCommandDefinition(ChatCommandRole.Mod, "!admin kick <player>", Kick),
        ["promote"] = new ChatCommandDefinition(ChatCommandRole.Admin, "!admin promote <player>", Promote),
        ["demote"] = new ChatCommandDefinition(ChatCommandRole.Admin, "!admin demote <player>", Demote),
        ["help"] = new ChatCommandDefinition(ChatCommandRole.Player, "!admin help", Help),
    };

    public static void Initialize(IZoneManager zoneManager, IDbContextFactory<DatabaseContext> dbContextFactory, ILogger adminLogger)
    {
        _zoneManager = zoneManager;
        _dbContextFactory = dbContextFactory;
        _adminLogger = adminLogger;
    }

    public static ChatCommandRole GetRole(Player player)
    {
        if (player.IsAdmin)
            return ChatCommandRole.Admin;

        if (player.IsMod)
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

    private static bool IsAuthorizedAgainstTarget(GatewayConnection connection, bool targetIsAdmin)
    {
        if (!targetIsAdmin)
        {
            return true;
        }
            
        ChatCommandRole role = GetRole(connection.Player);
        return role >= ChatCommandRole.Admin;
    }

    private static bool TryResolveTarget(GatewayConnection connection, DatabaseContext dbContext, string targetName, out ulong targetUserId)
    {
        var target = dbContext.Characters
            .Where(character => character.FullName == targetName)
            .Select(character => new { character.UserId, character.User.IsAdmin })
            .SingleOrDefault();

        if (target is null)
        {
            SendSystemMessage(connection, $"No player named \"{targetName}\" was found.");
            targetUserId = 0;
            return false;
        }

        if (!IsAuthorizedAgainstTarget(connection, target.IsAdmin))
        {
            SendSystemMessage(connection, "You don't have permission to target an admin.");
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

        if (GetRole(connection.Player) < command.RequiredRole)
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

        dbContext.Users
            .Where(x => x.Id == targetUserId)
            .ExecuteUpdate(x => x
                .SetProperty(u => u.IsLocked, true)
                .SetProperty(u => u.LockedUntil, banUntilTime));

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
                .SetProperty(u => u.IsLocked, false)
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
                .SetProperty(u => u.IsMuted, true)
                .SetProperty(u => u.MutedUntil, muteUntilTime));

        if (_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
        {
            targetPlayer.IsMuted = true;
            targetPlayer.MutedUntil = muteUntilTime;
        }

        LogAction(connection, "Mute", targetName, muteUntilTime is null ? "Permanent" : $"Until: {muteUntilTime:u}");

        SendSystemMessage(connection, muteUntilTime is null
            ? $"{targetName} has been muted."
            : $"{targetName} has been muted until {muteUntilTime:u}.");
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
                .SetProperty(u => u.IsMuted, false)
                .SetProperty(u => u.MutedUntil, (DateTimeOffset?)null));

        if (_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
        {
            targetPlayer.IsMuted = false;
            targetPlayer.MutedUntil = null;
        }

        LogAction(connection, "Unmute", targetName);

        SendSystemMessage(connection, $"{targetName} has been unmuted.");
    }

    private static void Kick(GatewayConnection connection, string[] args)
    {
        if (args.Length < 1)
        {
            SendSystemMessage(connection, $"Usage: {Commands["kick"].Usage}");
            return;
        }

        var targetName = string.Join(' ', args);

        if (IsSelfTarget(connection, targetName))
        {
            SendSystemMessage(connection, "You cannot kick yourself.");
            return;
        }

        if (!_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
        {
            SendSystemMessage(connection, $"{targetName} is not online.");
            return;
        }

        if (!IsAuthorizedAgainstTarget(connection, targetPlayer.IsAdmin))
        {
            SendSystemMessage(connection, "You don't have permission to target an admin.");
            return;
        }

        targetPlayer.Disconnect();

        LogAction(connection, "Kick", targetName);

        SendSystemMessage(connection, $"{targetName} has been kicked.");
    }

    private static void SetMod(GatewayConnection connection, string targetName, bool isMod)
    {
        if (GetRole(connection.Player) < ChatCommandRole.Admin)
        {
            SendSystemMessage(connection, "You don't have permission to use this command.");
            return;
        }

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        var target = dbContext.Characters.SingleOrDefault(x => x.FullName == targetName);

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
        ChatCommandRole role = GetRole(connection.Player);

        string[] usages = Commands.Values
            .Where(command => role >= command.RequiredRole)
            .OrderBy(command => command.Usage)
            .Select(command => command.Usage)
            .ToArray();

        foreach (var usage in usages)
            SendSystemMessage(connection, usage);
    }

    private static void SendSystemMessage(GatewayConnection connection, string message)
    {
        PacketChat packet = new PacketChat
        {
            Channel = ChatChannel.System,
            FromName = connection.Player.Name,
            ToName = connection.Player.Name,
            Message = message
        };

        connection.Player.SendTunneled(packet);
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