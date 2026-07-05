using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketAddFriendRequestHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketAddFriendRequestHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketAddFriendRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketAddFriendRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketAddFriendRequest), packet);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters.FirstOrDefault(x => x.FullName == packet.Name);

        if (dbCharacter is null)
        {
            SendSystemMessage(connection, "Player not found.");
            return true;
        }

        var requesterCharacterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var targetCharacterId = dbCharacter.Id;
        var targetGuid = GuidHelper.GetPlayerGuid(targetCharacterId);

        var requesterIsIgnoringTarget = dbContext.Ignores.Any(x =>
            x.CharacterId == requesterCharacterId &&
            x.IgnoreCharacterId == targetCharacterId);

        if (requesterIsIgnoringTarget)
        {
            SendSystemMessage(connection, "You cannot add a player you are ignoring.");
            return true;
        }

        if (!_zoneManager.TryGetPlayer(targetGuid, out var player))
        {
            SendSystemMessage(connection, "That player is not online.");
            return true;
        }

        if (player.Ignores.Any(x => x.Guid == connection.Player.Guid))
        {
            SendSystemMessage(connection, "You cannot add a player who is ignoring you.");
            return true;
        }

        var friendMessagePacket = new FriendMessagePacket
        {
            Type = FriendMessageType.FriendAddRequested,
            Guid = player.Guid,
            Name = player.Name
        };

        connection.SendTunneled(friendMessagePacket);

        var commandPacketConfirmFriendRequest = new CommandPacketConfirmFriendRequest
        {
            Guid = connection.Player.Guid,
            Name = connection.Player.Name
        };

        player.SendTunneled(commandPacketConfirmFriendRequest);

        return true;
    }

    private static void SendSystemMessage(GatewayConnection connection, string message)
    {
        connection.Player.SendTunneled(new PacketChat
        {
            Channel = ChatChannel.System,
            Message = message
        });
    }
}