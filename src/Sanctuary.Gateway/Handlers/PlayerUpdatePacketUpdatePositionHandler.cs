using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PlayerUpdatePacketUpdatePositionHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PlayerUpdatePacketUpdatePositionHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PlayerUpdatePacketUpdatePosition.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PlayerUpdatePacketUpdatePosition));
            return false;
        }

        // _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PlayerUpdatePacketUpdatePosition), packet);

        connection.Player.Mount?.UpdatePosition(packet.Position, packet.Rotation);
        connection.Player.UpdatePosition(packet.Position, packet.Rotation);

        connection.Player.SendTunneledToVisible(packet);

        return true;
    }
}