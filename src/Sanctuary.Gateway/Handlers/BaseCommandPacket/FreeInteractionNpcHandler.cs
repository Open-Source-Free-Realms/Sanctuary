using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FreeInteractionNpcHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(FreeInteractionNpcHandler));
        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FreeInteractionNpc.TryDeserialize(data, out var freeInteractionNpc))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(FreeInteractionNpc));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(FreeInteractionNpc), freeInteractionNpc);

        _questManager.RefreshObjectiveTarget(connection.Player);
        return true;
    }
}
