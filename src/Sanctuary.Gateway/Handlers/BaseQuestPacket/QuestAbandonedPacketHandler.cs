using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class QuestAbandonedPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(QuestAbandonedPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!QuestAbandonedPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(QuestAbandonedPacket));
            return false;
        }

        // Not sent by the retail client (journal "Drop Quest" uses CommandPacketQuestAbandon 26/23 instead) - kept as a defensive no-op.
        _logger.LogTrace("QuestAbandonedPacket (49/6) received: QuestId={q}", packet.QuestId);
        return true;
    }
}
