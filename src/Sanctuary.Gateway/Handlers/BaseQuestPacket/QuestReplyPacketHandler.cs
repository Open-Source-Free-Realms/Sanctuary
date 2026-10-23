using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class QuestReplyPacketHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(QuestReplyPacketHandler));

        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!QuestReplyPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(QuestReplyPacket));
            return false;
        }

        var player = connection.Player;

        if (packet.Accepted)
        {
            _questManager.AcceptQuest(player, packet.QuestId);
            return true;
        }

        if (_resourceManager.Quests.TryGet(packet.QuestId, out var quest))
        {
            player.LastInteractNpcGuid = player.Zone.Npcs.FirstOrDefault(npc => npc.DefinitionId == quest.GiverNpcId)?.Guid ?? 0;
            player.LastInteractAt = DateTime.UtcNow;
        }

        return true;
    }
}
