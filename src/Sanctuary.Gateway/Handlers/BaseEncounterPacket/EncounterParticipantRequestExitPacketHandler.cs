using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.MiniGames;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class EncounterParticipantRequestExitPacketHandler
{
    private static ILogger _logger = null!;
    private static IMiniGameManager _miniGameManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(EncounterParticipantRequestExitPacketHandler));
        _miniGameManager = serviceProvider.GetRequiredService<IMiniGameManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!EncounterParticipantRequestExitPacket.TryDeserialize(data, out var encounterParticipantRequestExitPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(EncounterParticipantRequestExitPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(EncounterParticipantRequestExitPacket), encounterParticipantRequestExitPacket);

        _miniGameManager.Leave(connection.Player);

        return true;
    }
}
