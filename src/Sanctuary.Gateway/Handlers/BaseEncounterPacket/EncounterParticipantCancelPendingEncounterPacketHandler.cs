using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Helpers;
using Sanctuary.Game.MiniGames;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class EncounterParticipantCancelPendingEncounterPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(EncounterParticipantCancelPendingEncounterPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!EncounterParticipantCancelPendingEncounterPacket.TryDeserialize(data, out var encounterParticipantCancelPendingEncounterPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(EncounterParticipantCancelPendingEncounterPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(EncounterParticipantCancelPendingEncounterPacket), encounterParticipantCancelPendingEncounterPacket);

        var miniGameUiSession = connection.Player.MiniGameUiSession;

        if (miniGameUiSession is null || miniGameUiSession.State != MiniGameUiState.Details)
            return true;

        MiniGameHelper.Close(connection.Player, miniGameUiSession);

        return true;
    }
}
