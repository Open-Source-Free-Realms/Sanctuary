using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ActivityPacketActivityListRefreshRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ActivityPacketActivityListRefreshRequestHandler));
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data, int serverType)
    {
        if (!ActivityPacketActivityListRefreshRequest.TryDeserialize(data, out var activityPacketActivityListRefreshRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ActivityPacketActivityListRefreshRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ActivityPacketActivityListRefreshRequest), activityPacketActivityListRefreshRequest);

        ActivityHelper.SendActivities(connection.Player, _resourceManager, serverType);

        return true;
    }
}
