using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.MiniGames;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketClosedMinigameEndScreenHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketClosedMinigameEndScreenHandler));
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketClosedMinigameEndScreen.TryDeserialize(data, out var commandPacketClosedMinigameEndScreen))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketClosedMinigameEndScreen));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketClosedMinigameEndScreen), commandPacketClosedMinigameEndScreen);

        var miniGameUiSession = connection.Player.MiniGameUiSession;

        if (miniGameUiSession is null || miniGameUiSession.State == MiniGameUiState.Details)
            return true;

        CombatEncounterHelper.Close(connection.Player, miniGameUiSession, _zoneManager);

        return true;
    }
}
