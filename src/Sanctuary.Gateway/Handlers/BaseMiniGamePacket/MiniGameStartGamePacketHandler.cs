using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        return true;
    }
}