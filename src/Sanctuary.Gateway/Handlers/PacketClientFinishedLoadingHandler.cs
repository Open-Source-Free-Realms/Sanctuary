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
        connection.Player.UpdatePosition(connection.Player.Position, connection.Player.Rotation);

        if (connection.Player.Mount is not null)
        {
            var mount = connection.Player.Mount;

            mount.Visible = true;
            mount.UpdatePosition(connection.Player.Position, connection.Player.Rotation);

            connection.Player.SendTunneled(new PacketMountResponse
            {
                RiderGuid = connection.Player.Guid,
                MountGuid = mount.Guid,
                Seat = mount.Seat,
                QueuePosition = mount.QueuePosition,
                Unknown = 1,
                CompositeEffectId = 46,
                NameVerticalOffset = mount.Definition.NameVerticalOffset
            });
        }

        connection.Player.Zone.OnClientFinishedLoading(connection.Player);

        return true;
    }
}