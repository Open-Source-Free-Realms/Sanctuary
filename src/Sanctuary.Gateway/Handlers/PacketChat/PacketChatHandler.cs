using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketChatHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketChatHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketChat.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketChat));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketChat), packet);

        packet.FromGuid = connection.Player.Guid;
        packet.FromName = connection.Player.Name;

        // TODO: Handle each message channel.

        connection.Player.SendTunneledToVisible(packet, true);

        return true;
    }
}