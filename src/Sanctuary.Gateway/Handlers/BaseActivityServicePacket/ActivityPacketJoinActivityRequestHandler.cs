using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.MiniGames;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ActivityPacketJoinActivityRequestHandler
{
    private static ILogger _logger = null!;
    private static IMiniGameManager _miniGameManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ActivityPacketJoinActivityRequestHandler));
        _miniGameManager = serviceProvider.GetRequiredService<IMiniGameManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ActivityPacketJoinActivityRequest.TryDeserialize(data, out var activityPacketJoinActivityRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ActivityPacketJoinActivityRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ActivityPacketJoinActivityRequest), activityPacketJoinActivityRequest);

        _miniGameManager.ShowDetails(connection.Player, activityPacketJoinActivityRequest.ActivityId);

        return true;
    }
}
