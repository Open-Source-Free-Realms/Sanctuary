using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketWorldTeleportRequestHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketWorldTeleportRequestHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PacketWorldTeleportRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketWorldTeleportRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketWorldTeleportRequest), packet);

        if (!_zoneManager.TryGetPlayer(packet.Guid, out var player))
            return true;

        var position = player.Position;
        var rotation = player.Rotation;

        connection.Player.Mount?.UpdatePosition(position, rotation);
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
