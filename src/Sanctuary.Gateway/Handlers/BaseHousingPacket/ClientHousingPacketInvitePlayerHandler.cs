using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketInvitePlayerHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketInvitePlayerHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketInvitePlayer.TryDeserialize(data, out var clientHousingPacketInvitePlayer))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketInvitePlayer));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketInvitePlayer), clientHousingPacketInvitePlayer);

        var player = connection.Player;

        if (player.Zone is not HousingZone { OwnerId: { } houseGuid } housingZone || !housingZone.IsOwner(player)
            || !_zoneManager.TryGetPlayer(clientHousingPacketInvitePlayer.InviteeGuid, out var invitee))
        {
            _logger.LogWarning("{player} could not invite {invitee}.", player.Guid, clientHousingPacketInvitePlayer.InviteeGuid);
            return true;
        }

        invitee.HouseInvites.Add(houseGuid);

        invitee.SendTunneled(new HousingPacketInviteNotification
        {
            Invite = new PendingHouseInvite
            {
                FromGuid = player.Guid,
                ToGuid = invitee.Guid,
                HouseGuid = houseGuid,
                Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                FromName = player.Name,
                ToName = invitee.Name
            }
        });

        return true;
    }
}