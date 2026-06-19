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
            return true;

        if (!_zoneManager.TryGetPlayer(GuidHelper.GetPlayerGuid(dbCharacter.Id), out var player))
            return true;

        if (player.Guid == connection.Player.Guid)
            return true;

        if (player.Ignores.Any(x => x.Guid == connection.Player.Guid))
            return true;

        if (player.Friends.Any(x => x.Guid == connection.Player.Guid))
            return true;

        player.IncomingFriendRequests.Add(connection.Player.Guid);

        var friendMessagePacket = new FriendMessagePacket();

        friendMessagePacket.Type = FriendMessageType.FriendAddRequested;

        friendMessagePacket.Guid = player.Guid;
        friendMessagePacket.Name = player.Name;

        connection.SendTunneled(friendMessagePacket);

        var commandPacketConfirmFriendRequest = new CommandPacketConfirmFriendRequest();

        commandPacketConfirmFriendRequest.Guid = connection.Player.Guid;
        commandPacketConfirmFriendRequest.Name = connection.Player.Name;

        player.SendTunneled(commandPacketConfirmFriendRequest);

        return true;
    }
}