using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketClientFinishedLoadingHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketClientFinishedLoadingHandler));
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        _logger.LogTrace("Received {name} packet.", nameof(PacketClientFinishedLoading));

        connection.Player.Visible = true;

        if (connection.Player.Mount is not null)
            connection.Player.Mount.Visible = true;

        connection.Player.UpdatePosition(connection.Player.Position, connection.Player.Rotation);

        if (connection.Player.Mount is not null)
        {
            connection.Player.SendTunneled(connection.Player.Mount.GetAddNpcPacket());
            connection.Player.SendTunneled(connection.Player.Mount.GetMountResponsePacket());
        }

        connection.Player.Zone.OnClientFinishedLoading(connection.Player);

        return true;
    }
}