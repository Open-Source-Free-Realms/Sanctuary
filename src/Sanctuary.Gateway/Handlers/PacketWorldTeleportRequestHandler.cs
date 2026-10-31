using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketWorldTeleportRequestHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketWorldTeleportRequestHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PacketWorldTeleportRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketWorldTeleportRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketWorldTeleportRequest), packet);

        if (!connection.Player.Friends.Any(friend => friend.Guid == packet.Guid))
            return true;

        if (!_zoneManager.TryGetPlayer(packet.Guid, out var player))
            return true;

        if (player.Zone != connection.Player.Zone)
        {
            TeleportToFriendZone(connection, player);
            return true;
        }

        var position = player.Position;
        var rotation = player.Rotation;

        connection.Player.UpdatePosition(position, rotation);

        connection.Player.UpdatePointOfInterest();

        var clientUpdatePacketUpdateLocation = new ClientUpdatePacketUpdateLocation
        {
            Position = position,
            Rotation = rotation,
            Teleport = true
        };

        connection.SendTunneled(clientUpdatePacketUpdateLocation);

        return true;
    }

    private static void TeleportToFriendZone(GatewayConnection connection, Player friend)
    {
        if (friend.Zone is HousingZone { OwnerId: { } houseGuid })
        {
            var housingError = HousingError.PermissionDenied;

            if (friend.IsPreviewingHouse || !HousingHelper.TryEnterHouse(_resourceManager, _dbContextFactory, connection.Player, houseGuid, out housingError))
                connection.SendTunneled(new HousingPacketHousingError { Error = housingError });

            return;
        }

        connection.Player.TeleportToZone(friend.Zone.DefinitionId, null, friend.Position, friend.Rotation);
    }
}