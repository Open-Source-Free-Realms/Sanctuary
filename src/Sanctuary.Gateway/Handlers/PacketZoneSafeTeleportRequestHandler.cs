using System;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketZoneSafeTeleportRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketZoneSafeTeleportRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PacketZoneSafeTeleportRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketZoneSafeTeleportRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketZoneSafeTeleportRequest), packet);

        var pointOfInterest = FindNearestSafePointOfInterest(connection.Player.Position);

        if (pointOfInterest is null)
        {
            _logger.LogWarning("No safe teleport destination found for player {guid}.", connection.Player.Guid);
            return true;
        }

        var rotationX = MathF.Cos(pointOfInterest.Heading);
        var rotationZ = MathF.Sin(pointOfInterest.Heading);

        var position = pointOfInterest.SpawnPosition;
        var rotation = new Quaternion(rotationZ, 0f, rotationX, 0f);

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

    private static PointOfInterestDefinition? FindNearestSafePointOfInterest(Vector4 playerPosition)
    {
        var hubPointsOfInterest = _resourceManager.PointOfInterests.Values
            .Where(x => x.NotificationType == PointOfInterestNotificationType.ZoneHub)
            .ToList();

        var candidates = hubPointsOfInterest.Count > 0
            ? hubPointsOfInterest
            : _resourceManager.PointOfInterests.Values.ToList();

        PointOfInterestDefinition? nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var pointOfInterest in candidates)
        {
            var dx = playerPosition.X - pointOfInterest.Position.X;
            var dz = playerPosition.Z - pointOfInterest.Position.Z;
            var distance = dx * dx + dz * dz;

            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = pointOfInterest;
        }

        return nearest;
    }
}
