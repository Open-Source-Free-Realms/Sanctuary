using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class QuickChatSendTellPacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(QuickChatSendTellPacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!QuickChatSendTellPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(QuickChatSendTellPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(QuickChatSendTellPacket), packet);

        if (!_zoneManager.TryGetPlayer(packet.ToName, out var toPlayer))
            return true;

        packet.Name = connection.Player.Name;

        toPlayer.SendTunneled(packet);

        return true;
    }
}