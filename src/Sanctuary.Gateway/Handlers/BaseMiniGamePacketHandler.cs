using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Game.Minigames;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// Minigame requests. Only the daily wheel is implemented: its start request and its Flash messages. Every
/// other minigame packet is logged in full, so the remaining layouts can be confirmed against the client.
/// </summary>
[PacketHandler]
public static class BaseMiniGamePacketHandler
{
    private static ILogger _logger = null!;
    private static IRewardManager _rewardManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseMiniGamePacketHandler));

        _rewardManager = serviceProvider.GetRequiredService<IRewardManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        var data = Convert.ToHexString(reader.Span);

        // The tunnel handler has read the opcode already; the header reader starts from it.
        reader.Reset();

        if (BaseMiniGamePacket.TryReadHeader(ref reader, out var subOpCode, out var stageId, out _))
        {
            // The wheel is the only minigame the server starts, so any start request is for it.
            if (subOpCode == BaseMiniGamePacket.RequestStartGame)
            {
                _logger.LogInformation("{player} started the daily wheel.", connection.Player.Name.FullName);

                DailyWheelGame.Start(connection.Player);
                return true;
            }

            if (subOpCode == BaseMiniGamePacket.Payload && stageId == BaseZone.DailyWheelMinigameId
                && reader.TryRead(out string payload))
            {
                _logger.LogInformation("{player} sent a daily wheel message. ( Payload: {payload}, Data: {data} )",
                    connection.Player.Name.FullName, payload.Replace('\t', '|'), data);

                DailyWheelGame.HandlePayload(connection.Player, payload, _rewardManager);
                return true;
            }
        }

        _logger.LogInformation("{player} sent a minigame packet. ( Data: {data} )", connection.Player.Name.FullName, data);

        return true;
    }
}
