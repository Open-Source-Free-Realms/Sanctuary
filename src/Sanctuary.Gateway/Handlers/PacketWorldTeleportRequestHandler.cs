using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Game;
using Sanctuary.Game.Housing;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketWorldTeleportRequestHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IHouseManager _houseManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketWorldTeleportRequestHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _houseManager = serviceProvider.GetRequiredService<IHouseManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PacketWorldTeleportRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketWorldTeleportRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketWorldTeleportRequest), packet);

        if (!_zoneManager.TryGetPlayer(packet.Guid, out var player) || !player.Visible)
            return true;

        var position = player.Position;
        var rotation = player.Rotation;

        if (!ReferenceEquals(connection.Player.Zone, player.Zone))
        {
            if (player.Zone is HousingZone housingZone)
            {
                _houseManager.EnterHouse(
                    connection.Player,
                    GuidHelper.GetHouseGuid(housingZone.HouseId));
                return true;
            }

            if (player.Zone is not WorldZone ||
                !connection.Player.TeleportToZone(player.Zone, position, rotation))
            {
                return true;
            }

            return true;
        }

        connection.Player.UpdatePosition(position, rotation, updateZoneArea: false);

        var clientUpdatePacketUpdateLocation = new ClientUpdatePacketUpdateLocation
        {
            Position = position,
            Rotation = rotation,
            Teleport = true
        };

        connection.SendTunneled(clientUpdatePacketUpdateLocation);

        return true;
    }
}
