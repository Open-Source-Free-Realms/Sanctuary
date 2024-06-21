using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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

        connection.Player.Position = packet.Position;
        connection.Player.Rotation = packet.Rotation;

        if (connection.Player.Mount is not null)
        {
            connection.Player.Mount.Position = packet.Position;
            connection.Player.Mount.Rotation = packet.Rotation;
        }

        connection.Player.SendTunneledToVisible(packet);

        return true;
    }
}