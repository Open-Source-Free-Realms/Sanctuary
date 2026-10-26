using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class SelectQuestPacketHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(SelectQuestPacketHandler));

        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!SelectQuestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(SelectQuestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(SelectQuestPacket), packet);

        _questManager.SetActiveQuest(connection.Player, packet.QuestId);
        return true;
    }
}
