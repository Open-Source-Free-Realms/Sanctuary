using System;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PlayerTitleRequestSelectPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PlayerTitleRequestSelectPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PlayerTitleRequestSelectPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PlayerTitleRequestSelectPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PlayerTitleRequestSelectPacket), packet);

        var selectedTitle = connection.Player.Titles.FirstOrDefault(x => x.Id == packet.Id);

        connection.Player.ActiveTitle = selectedTitle is null ? 0 : selectedTitle.Id;

        // Send an update to all players

        var playerUpdatePacketPlayerTitle = new PlayerUpdatePacketPlayerTitle();

        playerUpdatePacketPlayerTitle.Guid = connection.Player.Guid;

        if (selectedTitle is not null)
            playerUpdatePacketPlayerTitle.Title = selectedTitle;

        connection.Player.SendTunneledToVisible(playerUpdatePacketPlayerTitle, true);

        return true;
    }
}