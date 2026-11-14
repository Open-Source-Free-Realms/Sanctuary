using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Groups;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GroupMapPingPacketHandler
{
    private static ILogger _logger = null!;
    private static IGroupManager _groupManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GroupMapPingPacketHandler));

        _groupManager = serviceProvider.GetRequiredService<IGroupManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GroupMapPingPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GroupMapPingPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GroupMapPingPacket), packet);

        _groupManager.SendMapPing(connection.Player, packet.X, packet.Y);

        return true;
    }
}
