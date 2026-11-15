using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.MiniGames;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class EncounterParticipantCancelPendingEncounterPacketHandler
{
    private static ILogger _logger = null!;
    private static IMiniGameManager _miniGameManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(EncounterParticipantCancelPendingEncounterPacketHandler));
        _miniGameManager = serviceProvider.GetRequiredService<IMiniGameManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!EncounterParticipantCancelPendingEncounterPacket.TryDeserialize(data, out var encounterParticipantCancelPendingEncounterPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(EncounterParticipantCancelPendingEncounterPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(EncounterParticipantCancelPendingEncounterPacket), encounterParticipantCancelPendingEncounterPacket);

        _miniGameManager.CancelDetails(connection.Player);

        return true;
    }
}
