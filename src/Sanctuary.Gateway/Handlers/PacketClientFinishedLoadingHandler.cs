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

        var mount = connection.Player.Mount;

        if (mount is not null)
        {
            mount.Visible = true;

            mount.UpdatePosition(connection.Player.Position, connection.Player.Rotation);

            var packetMountResponse = new PacketMountResponse();

            packetMountResponse.RiderGuid = mount.Rider.Guid;
            packetMountResponse.MountGuid = mount.Guid;

            packetMountResponse.Seat = mount.Seat;

            packetMountResponse.QueuePosition = mount.QueuePosition;

            packetMountResponse.Unknown = 1;

            packetMountResponse.CompositeEffectId = 46; // PFX_Teleport_Flash

            // packetMountResponse.NameVerticalOffset = mountDefinition.NameVerticalOffset;

            connection.Player.SendTunneled(packetMountResponse);
        }

        connection.Player.Zone.OnClientFinishedLoading(connection.Player);

        return true;
    }
}