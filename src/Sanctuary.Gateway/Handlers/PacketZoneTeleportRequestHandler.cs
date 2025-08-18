using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketZoneTeleportRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketZoneTeleportRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PacketZoneTeleportRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketZoneTeleportRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketZoneTeleportRequest), packet);

        if (!_resourceManager.PointOfInterests.TryGetValue(packet.Id, out var pointOfInterest))
        {
            _logger.LogError("Invalid point of interest.");
            return true;
        }

        var rotationX = MathF.Cos(pointOfInterest.Heading);
        var rotationZ = MathF.Sin(pointOfInterest.Heading);

        var clientUpdatePacketUpdateLocation = new ClientUpdatePacketUpdateLocation
        {
            Position = new(pointOfInterest.X, pointOfInterest.Y, pointOfInterest.Z, 1f),
            Rotation = new(rotationZ, 0f, rotationX, 0f),
            Teleport = true
        };

        connection.SendTunneled(clientUpdatePacketUpdateLocation);

        return true;
    }
}