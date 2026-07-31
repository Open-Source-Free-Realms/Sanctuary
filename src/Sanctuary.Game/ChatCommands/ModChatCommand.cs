using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class ModChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "mod";

    public string Usage => "ban|mute <player> <minutes> | unban|unmute <player>";

    public string Description => "Moderation command for banning, muting, unbanning, or unmuting players.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public ModChatCommand(IChatCommandManager chatCommandManager, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager)
    {
        _chatCommandManager = chatCommandManager;
        _dbContextFactory = dbContextFactory;
        _zoneManager = zoneManager;
    }

    private void LogAction(Player invoker, string action, string targetName, string? detail = null)
    {
        _chatCommandManager.LogAction(this, invoker, action, targetName, detail);
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length < 1)
            return false;

        var subCommand = args[0].ToLower();

        args = args[1..];

        switch (subCommand)
        {
            case "ban":
                return Ban(invoker, args);
            case "mute":
                return Mute(invoker, args);
            case "unban":
                return Unban(invoker, args);
            case "unmute":
                return Unmute(invoker, args);
            default:
                return false;
        }
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

    private static bool IsSelfTarget(Player invoker, string targetName)
    {
        return invoker.Name.FullName == targetName;
    }

    private static bool IsAuthorizedAgainstTarget(ChatCommandRole playerRole, ChatCommandRole targetRole)
    {
        return playerRole > targetRole;
    }

    private static bool TryResolveTarget(Player invoker, DatabaseContext dbContext, string targetName, out ulong targetUserId)
    {
        var target = dbContext.Characters
            .Where(character => character.FullName == targetName)
            .Select(character => new { character.UserId, character.User.IsAdmin, character.User.IsMod })
            .SingleOrDefault();

        if (target is null)
        {
            ChatHelper.SendSystemMessage(invoker, $"No player named \"{targetName}\" was found.");
            targetUserId = 0;
            return false;
        }

        ChatCommandRole targetRole = ChatHelper.GetRoleFromFlags(target.IsAdmin, target.IsMod);
        if (!IsAuthorizedAgainstTarget(invoker.ChatCommandRole, targetRole))
        {
            ChatHelper.SendSystemMessage(invoker, "You don't have permission to target this player.");
            targetUserId = 0;
            return false;
        }

        targetUserId = target.UserId;
        return true;
    }

    private bool Ban(Player invoker, string[] args)
    {
        if (!TryParseTarget(args, out var targetName, out var banUntilTime, out var error))
            return false;

        if (IsSelfTarget(invoker, targetName))
        {
            ChatHelper.SendSystemMessage(invoker, "You cannot ban yourself.");
            return true;
        }

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        if (!TryResolveTarget(invoker, dbContext, targetName, out var targetUserId))
            return false;

        DateTimeOffset lockedUntil = banUntilTime ?? DateTimeOffset.MaxValue;
        dbContext.Users
            .Where(user => user.Id == targetUserId)
            .ExecuteUpdate(user => user
                .SetProperty(u => u.LockedUntil, lockedUntil));

        if (_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
            targetPlayer.Disconnect();

        LogAction(invoker, "Ban", targetName, banUntilTime is null ? "Permanent" : $"Until: {banUntilTime:u}");

        ChatHelper.SendSystemMessage(invoker, banUntilTime is null
            ? $"{targetName} has been banned permanently."
            : $"{targetName} has been banned until {banUntilTime:u}.");
        return true;
    }

    private bool Unban(Player invoker, string[] args)
    {
        if (args.Length < 1)
            return false;

        string targetName = string.Join(' ', args);

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        if (!TryResolveTarget(invoker, dbContext, targetName, out var targetUserId))
            return false;

        dbContext.Users
            .Where(user => user.Id == targetUserId)
            .ExecuteUpdate(user => user
                .SetProperty(u => u.LockedUntil, (DateTimeOffset?)null));

        LogAction(invoker, "Unban", targetName);

        ChatHelper.SendSystemMessage(invoker, $"{targetName} has been unbanned.");
        return true;
    }

    private bool Mute(Player invoker, string[] args)
    {
        if (!TryParseTarget(args, out var targetName, out var muteUntilTime, out var error))
            return false;

        if (muteUntilTime == null)
        {
            ChatHelper.SendSystemMessage(invoker, $"Please specify a duration in minutes for mute.");
            return false;
        }

        if (IsSelfTarget(invoker, targetName))
        {
            ChatHelper.SendSystemMessage(invoker, "You cannot mute yourself.");
            return true;
        }

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        if (!TryResolveTarget(invoker, dbContext, targetName, out var targetUserId))
            return false;

        dbContext.Users
            .Where(user => user.Id == targetUserId)
            .ExecuteUpdate(user => user
                .SetProperty(u => u.MutedUntil, muteUntilTime));

        if (_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
        {
            targetPlayer.MutedUntil = muteUntilTime;
        }

        LogAction(invoker, "Mute", targetName, $"Until: {muteUntilTime:u}");

        ChatHelper.SendSystemMessage(invoker, $"{targetName} has been muted until {muteUntilTime:u}.");
        return true;
    }

    private bool Unmute(Player invoker, string[] args)
    {
        if (args.Length < 1)
            return false;

        string targetName = string.Join(' ', args);

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        if (!TryResolveTarget(invoker, dbContext, targetName, out var targetUserId))
            return false;

        dbContext.Users
            .Where(user => user.Id == targetUserId)
            .ExecuteUpdate(user => user
                .SetProperty(u => u.MutedUntil, (DateTimeOffset?)null));

        if (_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
        {
            targetPlayer.MutedUntil = null;
        }

        LogAction(invoker, "Unmute", targetName);

        ChatHelper.SendSystemMessage(invoker, $"{targetName} has been unmuted.");
        return true;
    }
}