using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GuildInviteTimeOutPacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildInviteTimeOutPacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildInviteTimeOutPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildInviteTimeOutPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildInviteTimeOutPacket), packet);

        if (!_zoneManager.TryGetPlayer(packet.PlayerGuid, out var player))
            return true;

        player.SendTunneled(new GuildErrorPacket
        {
            MessageName = "GuildInviteTimedOut"
        });

        return true;
    }
}