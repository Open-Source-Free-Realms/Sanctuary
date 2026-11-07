using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketFriendsPositionRequestHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketFriendsPositionRequestHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        var friendUpdatePositionsPacket = new FriendUpdatePositionsPacket();

        var friends = connection.Player.Zone == _zoneManager.StartingZone ? connection.Player.Friends : [];

        foreach (var friend in friends)
        {
            if (!_zoneManager.TryGetPlayer(friend.Guid, out var friendPlayer))
                continue;

            if (friendPlayer.Zone != _zoneManager.StartingZone)
            {
                friendUpdatePositionsPacket.Entries.Add(new FriendUpdatePositionsPacket.Entry
                {
                    Guid = friendPlayer.Guid,
                    InEncounter = true
                });

                continue;
            }

            if (!friendPlayer.Visible)
                continue;

            friendUpdatePositionsPacket.Entries.Add(new FriendUpdatePositionsPacket.Entry
            {
                Guid = friendPlayer.Guid,

                LocationX = friendPlayer.Position.X,
                LocationZ = friendPlayer.Position.Z
            });
        }

        connection.SendTunneled(friendUpdatePositionsPacket);

        return true;
    }
}