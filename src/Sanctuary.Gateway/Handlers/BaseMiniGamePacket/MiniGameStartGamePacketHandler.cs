using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Gateway.Fishing;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class MiniGameStartGamePacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(MiniGameStartGamePacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!MiniGameStartGamePacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(MiniGameStartGamePacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(MiniGameStartGamePacket), packet);

        var miniGameGameStarted = new MiniGameGameStartPacket(packet.StateId, packet.GroupId, packet.GameId);

        connection.SendTunneled(miniGameGameStarted);

        // Mining Practice
        if (packet.StateId == 1113)
        {
            var commandPacketStartFlashGame = new CommandPacketStartFlashGame()
            {
                LuaClass = "MiniGameFlash",
                Swf = "game_hidden.gfx"
            };

            connection.SendTunneled(commandPacketStartFlashGame);
        }
        else if (FishingActivityZones.TryGet(packet.StateId, out var fishingZone))
        {
            var packetClientBeginZoning = new PacketClientBeginZoning
            {
                Name = fishingZone.ZoneName,
                Type = 2,
                Position = fishingZone.SpawnPosition,
                Rotation = fishingZone.SpawnRotation,
                Sky = fishingZone.Sky,
                Unknown = 1,
                Id = packet.StateId,
                GeometryId = 214,
                OverrideUpdateRadius = true
            };

            connection.SendTunneled(packetClientBeginZoning);

            connection.SendTunneled(new PlayerUpdatePacketUpdateCharacterState
            {
                Guid = connection.Player.Guid,
                State = FishingActivityZones.FishingCharacterState
            });

            // Create/refresh the fishing session and record which zone the player entered so the
            // RegisterPlayerResponse can report the correct zone config.
            var session = FishingSessions.GetOrCreate(connection.Player);
            session.SetZone(packet.StateId, fishingZone);
            session.Reset();

            _logger.LogInformation(
                "Started fishing minigame activity {activityId}, zoning to {zoneName}",
                packet.StateId,
                fishingZone.ZoneName);
        }

        return true;
    }
}