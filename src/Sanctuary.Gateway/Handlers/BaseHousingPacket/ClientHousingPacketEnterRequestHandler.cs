using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketEnterRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketEnterRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketEnterRequest.TryDeserialize(data, out var clientHousingPacketEnterRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketEnterRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketEnterRequest), clientHousingPacketEnterRequest);

        var player = connection.Player;

        if (!HousingHelper.TryGetOwnedHouse(_resourceManager, player, clientHousingPacketEnterRequest.HouseGuid, out var houseDefinition)
            || !HousingHelper.CanEnterHouseZone(_resourceManager, player, houseDefinition.ZoneId)
            || !_resourceManager.Zones.TryGetValue(houseDefinition.ZoneId, out var zoneDefinition))
        {
            _logger.LogWarning("{player} requested a house they do not own. ( {houseGuid} )", player.Guid, clientHousingPacketEnterRequest.HouseGuid);
            return true;
        }

        if (!player.TeleportToZone(zoneDefinition.Id, player.Guid, zoneDefinition.SpawnPosition, zoneDefinition.SpawnRotation))
            _logger.LogWarning("{player} could not enter house {house}.", player.Guid, houseDefinition.Id);

        return true;
    }
}
