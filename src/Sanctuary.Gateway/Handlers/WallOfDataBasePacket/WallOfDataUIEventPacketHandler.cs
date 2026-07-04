using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class WallOfDataUIEventPacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(WallOfDataUIEventPacketHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!WallOfDataUIEventPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(WallOfDataUIEventPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(WallOfDataUIEventPacket), packet);

        if (packet.TableName == "ChatLog" &&
    packet.Callback == "sendChatMessage" &&
    packet.Param is not null && packet.Param.StartsWith("/pos"))
        {
            var chatPacketDebugChat = new ChatPacketDebugChat
            {
                Message = $"Position (X,Y,Z): {connection.Player.Position.X} {connection.Player.Position.Y} {connection.Player.Position.Z}",
                PrintToChat = true
            };

            connection.SendTunneled(chatPacketDebugChat);
        }

        return true;
    }
}