using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketSetChatBubbleColorHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketSetChatBubbleColorHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketSetChatBubbleColor.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketSetChatBubbleColor));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketSetChatBubbleColor), packet);

        connection.Player.ChatBubbleForegroundColor = packet.ChatBubbleForegroundColor;
        connection.Player.ChatBubbleBackgroundColor = packet.ChatBubbleBackgroundColor;
        connection.Player.ChatBubbleSize = packet.ChatBubbleSize;

        packet.Guid = connection.Player.Guid;

        connection.Player.SendTunneledToVisible(packet);

        return true;
    }
}