using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ActivityPacketJoinActivityRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ActivityPacketJoinActivityRequestHandler));
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ActivityPacketJoinActivityRequest.TryDeserialize(data, out var activityPacketJoinActivityRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ActivityPacketJoinActivityRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ActivityPacketJoinActivityRequest), activityPacketJoinActivityRequest);

        if (!_resourceManager.Activities.TryGet(activityPacketJoinActivityRequest.ActivityId, out var clientActivityDefinition) ||
            !clientActivityDefinition.PlayerCanJoin)
            return true;

        var definition = ActivityHelper.GetMiniGameUiDefinition(_resourceManager, clientActivityDefinition);

        CombatEncounterHelper.TryShowDetails(connection.Player, definition, _resourceManager);

        return true;
    }
}
