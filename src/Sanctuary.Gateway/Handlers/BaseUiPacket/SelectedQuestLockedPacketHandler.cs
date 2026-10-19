using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class SelectedQuestLockedPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(SelectedQuestLockedPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!SelectedQuestLockedPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(SelectedQuestLockedPacket));
            return false;
        }

        _logger.LogDebug("{player}'s tracked quest (QuestId={questId}) locked-state changed to {isLocked}.",
            connection.Player.Name, connection.Player.ActiveQuestId, packet.IsLocked);

        return true;
    }
}
