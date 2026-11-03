using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class QuestEndReplyPacketHandler
{
    private const int QuestEndBlockedTextId = 0;

    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(QuestEndReplyPacketHandler));

        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!QuestEndReplyPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(QuestEndReplyPacket));
            return false;
        }

        var player = connection.Player;

        var pendingQuestId = player.Quests.PendingTurnInQuestId;
        player.Quests.PendingTurnInQuestId = 0;

        if (pendingQuestId == 0 || pendingQuestId != packet.QuestId || !_questManager.CompleteQuest(player, packet.QuestId))
        {
            player.SendTunneled(new QuestEndBlockedPacket
            {
                TextId = QuestEndBlockedTextId,
                QuestId = packet.QuestId
            });
            return true;
        }

        if (!_questManager.OfferNextQuest(player, packet.QuestId))
            player.SendTunneled(new CommandPacketQuestDialogComplete());

        return true;
    }
}
