using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketZoneSafeTeleportRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketZoneSafeTeleportRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PacketZoneSafeTeleportRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketZoneSafeTeleportRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketZoneSafeTeleportRequest), packet);

        var clientUpdatePacketUpdateLocation = new ClientUpdatePacketUpdateLocation
        {
            Position = new(-1414.636f, -27.631f, 351.567f, 1f),
            Rotation = new(0f, 0f, 0f, 0f),
            Teleport = true
        };

        connection.SendTunneled(clientUpdatePacketUpdateLocation);

        return true;
    }
}