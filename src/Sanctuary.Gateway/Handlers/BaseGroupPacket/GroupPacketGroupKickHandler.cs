using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Groups;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GroupPacketGroupKickHandler
{
    private static ILogger _logger = null!;
    private static IGroupManager _groupManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GroupPacketGroupKickHandler));

        _groupManager = serviceProvider.GetRequiredService<IGroupManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GroupPacketGroupKick.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GroupPacketGroupKick));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GroupPacketGroupKick), packet);

        _groupManager.Kick(connection.Player, packet.TargetName.FullName);

        return true;
    }
}
