using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class EncounterParticipantRequestEntrancePacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(EncounterParticipantRequestEntrancePacketHandler));
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!EncounterParticipantRequestEntrancePacket.TryDeserialize(data, out var encounterParticipantRequestEntrancePacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(EncounterParticipantRequestEntrancePacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(EncounterParticipantRequestEntrancePacket), encounterParticipantRequestEntrancePacket);

        var miniGameUiSession = connection.Player.MiniGameUiSession;

        if (miniGameUiSession is null || miniGameUiSession.EncounterId != encounterParticipantRequestEntrancePacket.MiniGameStateId)
            return true;

        CombatEncounterHelper.Go(connection.Player, miniGameUiSession, _zoneManager);

        return true;
    }
}
