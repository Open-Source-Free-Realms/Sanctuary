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
public static class ChangeNameRequestPacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ChangeNameRequestPacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ChangeNameRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ChangeNameRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ChangeNameRequestPacket), packet);

        if (connection.Player.Guid != packet.Guid)
        {
            _logger.LogError("Invalid player guid. {guid}", packet.Guid);
        }

        var nameChangeResponsePacket = new NameChangeResponsePacket();

        nameChangeResponsePacket.Type = packet.Type;
        nameChangeResponsePacket.Guid = packet.Guid;
        nameChangeResponsePacket.Name = packet.Name;

        nameChangeResponsePacket.Result = packet.Type switch
        {
            NameChangeType.Character => OnChangeCharacterName(connection, packet),
            _ => ChangeNameResponse.Error
        };

        connection.SendTunneled(nameChangeResponsePacket);

        return true;
    }

    private static ChangeNameResponse OnChangeCharacterName(GatewayConnection connection, ChangeNameRequestPacket packet)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters.FirstOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbCharacter is null)
            return ChangeNameResponse.Error;

        dbCharacter.FirstName = packet.Name.FirstName;
        dbCharacter.LastName = packet.Name.LastName;

        if (dbContext.SaveChanges() <= 0)
            return ChangeNameResponse.Error;

        connection.Player.Name.FirstName = packet.Name.FirstName;
        connection.Player.Name.LastName = packet.Name.LastName;

        var playerUpdatePacketRenamePlayer = new PlayerUpdatePacketRenamePlayer();

        playerUpdatePacketRenamePlayer.Guid = connection.Player.Guid;
        playerUpdatePacketRenamePlayer.Name = connection.Player.Name;

        connection.Player.SendTunneledToVisible(playerUpdatePacketRenamePlayer, true);

        var friendRenamePacket = new FriendRenamePacket
        {
            Guid = connection.Player.Guid,
            Name = connection.Player.Name.FullName
        };

        foreach (var friend in connection.Player.Friends)
        {
            if (!_zoneManager.TryGetPlayer(friend.Guid, out var friendPlayer))
                continue;

            friendPlayer.SendTunneled(friendRenamePacket);
        }

        return ChangeNameResponse.Pending;
    }
}