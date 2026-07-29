using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

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

    private static void SendSystemMessage(GatewayConnection connection, string message)
    {
        ChatHelper.SendSystemMessage(connection.Player, message);
    }
}