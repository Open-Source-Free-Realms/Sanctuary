using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PlayerUpdatePacketJumpHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PlayerUpdatePacketJumpHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PlayerUpdatePacketJump.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PlayerUpdatePacketJump));
            return false;
        }

        // _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PlayerUpdatePacketJump), packet);

        connection.Player.Position = packet.Position;
        connection.Player.Rotation = packet.Rotation;

        connection.Player.SendTunneledToVisible(packet);

        return true;
    }
}