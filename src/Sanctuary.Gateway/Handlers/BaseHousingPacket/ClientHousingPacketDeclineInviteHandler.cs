using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketDeclineInviteHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketDeclineInviteHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketDeclineInvite.TryDeserialize(data, out var clientHousingPacketDeclineInvite))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketDeclineInvite));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketDeclineInvite), clientHousingPacketDeclineInvite);

        var player = connection.Player;

        _logger.LogInformation("{player} declined a house invite. ( {guid} )", player.Guid, clientHousingPacketDeclineInvite.Guid);

        player.HouseInvites.RemoveWhere(houseGuid => houseGuid == clientHousingPacketDeclineInvite.Guid || HousingHelper.GetHouseOwnerGuid(houseGuid) == clientHousingPacketDeclineInvite.Guid);

        var inviterGuid = _zoneManager.TryGetPlayer(clientHousingPacketDeclineInvite.Guid, out _) ? clientHousingPacketDeclineInvite.Guid : HousingHelper.GetHouseOwnerGuid(clientHousingPacketDeclineInvite.Guid);

        if (!_zoneManager.TryGetPlayer(inviterGuid, out var inviter))
            return true;

        inviter.SendTunneled(new HousingPacketDeclineNotification
        {
            Guid = player.Guid,
            Guid2 = inviter.Guid,
            Name = player.Name
        });

        return true;
    }
}