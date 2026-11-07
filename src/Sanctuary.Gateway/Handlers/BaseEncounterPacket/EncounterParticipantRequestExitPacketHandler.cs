using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class EncounterParticipantRequestExitPacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(EncounterParticipantRequestExitPacketHandler));
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!EncounterParticipantRequestExitPacket.TryDeserialize(data, out var encounterParticipantRequestExitPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(EncounterParticipantRequestExitPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(EncounterParticipantRequestExitPacket), encounterParticipantRequestExitPacket);

        var miniGameUiSession = connection.Player.MiniGameUiSession;

        if (miniGameUiSession is null)
            return true;

        CombatEncounterHelper.Close(connection.Player, miniGameUiSession, _zoneManager);

        return true;
    }
}
